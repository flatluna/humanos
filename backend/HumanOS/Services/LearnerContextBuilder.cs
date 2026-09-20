using System.Text;
using System.Text.Json;
using HumanOS.Contracts.EmployeeScoring;
using HumanOS.Data;
using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Service responsible for constructing the personalized LEARNER CONTEXT block
/// and guardrail instructions to be injected at the top of the Realtime Voice Tutor system prompt.
/// Strictly READ-ONLY and non-intrusive: if no profile or score exists, returns null.
/// </summary>
public interface ILearnerContextBuilder
{
    /// <summary>
    /// Builds the complete LEARNER CONTEXT and Personalization Instructions string,
    /// or null if no learner context is available.
    /// </summary>
    Task<string?> BuildLearnerContextBlockAsync(
        Guid? employeeId,
        Guid? courseId,
        string? engramId,
        CancellationToken cancellationToken = default,
        string? engramTitle = null);
}

public sealed class LearnerContextBuilder : ILearnerContextBuilder
{
    private readonly EmployeeScoringProfileService _profileService;
    private readonly HumanOsDbContext _dbContext;

    public LearnerContextBuilder(
        EmployeeScoringProfileService profileService,
        HumanOsDbContext dbContext)
    {
        _profileService = profileService;
        _dbContext = dbContext;
    }

    private sealed class HypothesisQuestionData
    {
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = [];
        public int? CorrectIndex { get; set; }
    }

    private static List<HypothesisQuestionData> ParseHypothesisStepQuestions(string? stepDataJson)
    {
        if (string.IsNullOrWhiteSpace(stepDataJson)) return [];
        try
        {
            using var doc = JsonDocument.Parse(stepDataJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<HypothesisQuestionData>>(stepDataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            }
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("HypothesisQuestions", out var arrayProp))
            {
                return JsonSerializer.Deserialize<List<HypothesisQuestionData>>(arrayProp.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            }
        }
        catch { }
        return [];
    }

    public async Task<string?> BuildLearnerContextBlockAsync(
        Guid? employeeId,
        Guid? courseId,
        string? engramId,
        CancellationToken cancellationToken = default,
        string? engramTitle = null)
    {
        if (!employeeId.HasValue || employeeId.Value == Guid.Empty)
        {
            return null;
        }

        EmployeeScoringProfile profile;
        try
        {
            profile = await _profileService.BuildProfileAsync(employeeId.Value, cancellationToken);
        }
        catch
        {
            return null;
        }

        // If the profile has no meaningful role/data, fallback to unpersonalized mode
        if (string.IsNullOrWhiteSpace(profile.RoleTitle) &&
            profile.ActiveObjectives.Count == 0 &&
            profile.TechnicalSkills.Count == 0 &&
            profile.Tools.Count == 0)
        {
            return null;
        }

        EngramImpactScore? impactScore = null;
        if (courseId.HasValue && courseId.Value != Guid.Empty && !string.IsNullOrWhiteSpace(engramId))
        {
            try
            {
                impactScore = await _dbContext.EngramImpactScores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        s => s.EmployeeId == employeeId.Value &&
                             s.CourseId == courseId.Value &&
                             s.EngramId == engramId,
                        cancellationToken);
            }
            catch
            {
                impactScore = null;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== LEARNER CONTEXT ===");

        // 0. The engram/lesson title being taught right now (2026-09-03) — the
        // model needs this to judge whether an objective is ACTUALLY thematically
        // related to this content, instead of forcing an arbitrary connection.
        if (!string.IsNullOrWhiteSpace(engramTitle))
        {
            sb.AppendLine($"- Current Engram/Lesson Title (what you are teaching right now): \"{engramTitle.Trim()}\"");
        }

        // 1. Name & Role + Experience — a real Tec de Monterrey student (StudentAcademicProfileSummary
        // present) is NEVER framed as a HumanOS employee doing a job, even if a placeholder/demo
        // JobRole happens to be assigned to their account — that would wrongly make the tutor think
        // their objectives are workplace tasks instead of their actual coursework.
        var isStudent = !string.IsNullOrWhiteSpace(profile.StudentAcademicProfileSummary);
        var name = !string.IsNullOrWhiteSpace(profile.EmployeeName) ? profile.EmployeeName.Trim() : "Learner";
        sb.AppendLine($"- Learner: {name}");
        if (isStudent)
        {
            sb.AppendLine($"- IMPORTANT: this learner is a real STUDENT at Tecnológico de Monterrey, NOT a HumanOS employee.");
            sb.AppendLine($"- Their real academic record: {profile.StudentAcademicProfileSummary}");
            sb.AppendLine("- Never reference a job role, workplace objectives, KPIs, or \"your role at HumanOS\" for this learner — they are here as a student, not an employee.");
        }
        else
        {
            var experienceStr = profile.YearsOfExperience.HasValue ? $" ({profile.YearsOfExperience.Value}+ years of experience)" : string.Empty;
            var roleStr = !string.IsNullOrWhiteSpace(profile.RoleTitle) ? profile.RoleTitle.Trim() : "Professional";
            sb.AppendLine($"- Role: {roleStr}{experienceStr}");

            if (!string.IsNullOrWhiteSpace(profile.RolePurpose))
            {
                sb.AppendLine($"- Role Mission: {profile.RolePurpose.Trim()}");
            }
        }

        // 2. Tier, PrimarySignal, Reason for THIS Engram — this scoring/rationale text was
        // generated by a SEPARATE employee-role-based system (EngramImpactScore) and can contain
        // stale references to a job role/objectives even for a real student, so skip it entirely
        // for students instead of injecting an un-vetted "your role" rationale verbatim.
        if (!isStudent && impactScore is not null)
        {
            var tierDisplay = !string.IsNullOrWhiteSpace(impactScore.Tier) ? impactScore.Tier : "Standard";
            var signalDisplay = !string.IsNullOrWhiteSpace(impactScore.PrimarySignal) ? $" (Primary Driver: {impactScore.PrimarySignal})" : string.Empty;
            sb.AppendLine($"- This Engram Strategic Priority: {tierDisplay} Impact{signalDisplay}");

            if (!string.IsNullOrWhiteSpace(impactScore.Reason))
            {
                sb.AppendLine($"- Personalization Rationale: \"{impactScore.Reason.Trim()}\"");
            }
        }

        // 2b. Student Initial Hypothesis / Intuition (if answered)
        if (!string.IsNullOrWhiteSpace(engramId) && Guid.TryParse(engramId, out var engramGuid))
        {
            try
            {
                // Check Structured Learning Hypothesis first
                var hypothesisResponses = await _dbContext.StructuredLearningHypothesisResponses
                    .AsNoTracking()
                    .Where(r => r.StructuredLearningDocumentSectionId == engramGuid && r.PersonId == employeeId.Value)
                    .OrderBy(r => r.QuestionIndex)
                    .ToListAsync(cancellationToken);

                if (hypothesisResponses.Count > 0)
                {
                    var step = await _dbContext.StructuredLearningSectionSteps
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            s => s.StructuredLearningDocumentSectionId == engramGuid &&
                                 s.StepType == Models.Capabilities.Studio.StructuredLearningStepType.Hypothesis,
                            cancellationToken);

                    var parsedQuestions = ParseHypothesisStepQuestions(step?.StepDataJson);
                    sb.AppendLine("- Student Initial Hypothesis / Intuition (Before Learning):");
                    foreach (var resp in hypothesisResponses)
                    {
                        var qText = (resp.QuestionIndex >= 0 && resp.QuestionIndex < parsedQuestions.Count)
                            ? parsedQuestions[resp.QuestionIndex].Question
                            : $"Question {resp.QuestionIndex + 1}";

                        var optText = (resp.QuestionIndex >= 0 && resp.QuestionIndex < parsedQuestions.Count &&
                                       resp.SelectedOptionIndex >= 0 && resp.SelectedOptionIndex < parsedQuestions[resp.QuestionIndex].Options.Count)
                            ? parsedQuestions[resp.QuestionIndex].Options[resp.SelectedOptionIndex]
                            : $"Option {resp.SelectedOptionIndex}";

                        var status = resp.IsCorrect.HasValue
                            ? (resp.IsCorrect.Value ? "Correct prediction" : "Incorrect initial guess (productive misconception)")
                            : "Submitted intuition";

                        sb.AppendLine($"  * {qText} -> Selected: \"{optText}\" ({status})");
                    }
                }
                else
                {
                    // Check Graph Learning Evidence for Hypothesis
                    var graphHypothesisEvidence = await _dbContext.LearningSessionSteps
                        .AsNoTracking()
                        .Where(s => s.LearningSessionNode!.CapabilityGraphNodeId == engramGuid &&
                                    s.LearningSessionNode.LearningSession!.PersonId == employeeId.Value &&
                                    s.StepType == Models.Capabilities.Graph.ExperienceStepType.Hypothesis)
                        .SelectMany(s => s.Evidence)
                        .OrderByDescending(e => e.CreatedDate)
                        .Select(e => e.StudentResponse)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (!string.IsNullOrWhiteSpace(graphHypothesisEvidence))
                    {
                        sb.AppendLine($"- Student Initial Hypothesis / Intuition (Before Learning): \"{graphHypothesisEvidence.Trim()}\"");
                    }
                }
            }
            catch
            {
                // Defensive
            }
        }

        // 3. Active SMART Objectives — ALL of them (2026-09-03, explicit user
        // request: the agent must know every one of the employee's objectives,
        // not just the top 3), so it can explain how THIS Engram helps achieve
        // them and why studying it matters. Never for a real student profile.
        if (!isStudent && profile.ActiveObjectives.Count > 0)
        {
            sb.AppendLine("- Active SMART Objectives:");
            foreach (var obj in profile.ActiveObjectives)
            {
                var metricPart = !string.IsNullOrWhiteSpace(obj.Metric) ? $" [Target: {obj.Metric}]" : string.Empty;
                var quarterPart = !string.IsNullOrWhiteSpace(obj.Quarter) ? $" ({obj.Quarter})" : string.Empty;
                sb.AppendLine($"  * {obj.Title}{metricPart}{quarterPart}");
                // Short description only (ValueImpact) — never the ActionPlan/
                // MeasurementMethod/etc. fields, those are too granular for the
                // model to judge topical relevance from.
                if (!string.IsNullOrWhiteSpace(obj.ValueImpact))
                {
                    sb.AppendLine($"    {obj.ValueImpact.Trim()}");
                }
            }
        }

        // 4. Performance KPIs
        if (!isStudent && profile.Kpis.Count > 0)
        {
            sb.AppendLine("- Measured Role KPIs:");
            foreach (var kpi in profile.Kpis.Take(3))
            {
                sb.AppendLine($"  * {kpi}");
            }
        }

        // 5. Relevant Coaching Notes & Growth Areas
        if (!isStudent && profile.CoachingNotes.Count > 0)
        {
            sb.AppendLine("- Manager Coaching & Focus Areas:");
            foreach (var coach in profile.CoachingNotes.Take(3))
            {
                var competency = !string.IsNullOrWhiteSpace(coach.CompetencyName) ? coach.CompetencyName : coach.CompetencyType;
                var note = !string.IsNullOrWhiteSpace(coach.CoachingNote) ? $" — \"{coach.CoachingNote}\"" : string.Empty;
                sb.AppendLine($"  * {competency}{note}");
            }
        }

        // 6. Tools & Technologies (for concrete examples) — role/job-derived, skip for students.
        var combinedTools = profile.Tools
            .Concat(profile.TechnicalSkills.Take(5))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!isStudent && combinedTools.Count > 0)
        {
            sb.AppendLine($"- Known Tools & Technical Stack: {string.Join(", ", combinedTools.Take(8))}");
        }

        // 7. Gap between Role Demands vs Demonstrated Resume — also role-derived, skip for students.
        var roleDemands = profile.TechnicalSkills
            .Concat(profile.CoreResponsibilities.Take(3))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resumeDemonstrated = profile.ResumeSkills
            .Concat(profile.Certifications)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!isStudent && roleDemands.Count > 0 && resumeDemonstrated.Count > 0)
        {
            var verifiedOverlap = roleDemands.Intersect(resumeDemonstrated, StringComparer.OrdinalIgnoreCase).ToList();
            var unverifiedGaps = roleDemands.Except(resumeDemonstrated, StringComparer.OrdinalIgnoreCase).ToList();

            if (unverifiedGaps.Count > 0)
            {
                sb.AppendLine($"- Unverified Role Growth Areas: {string.Join(", ", unverifiedGaps.Take(4))}");
            }
            if (verifiedOverlap.Count > 0)
            {
                sb.AppendLine($"- Verified Prior Strengths in CV: {string.Join(", ", verifiedOverlap.Take(4))}");
            }
        }
        else if (!isStudent && !string.IsNullOrWhiteSpace(profile.ResumeSummary))
        {
            sb.AppendLine($"- Background Summary: {profile.ResumeSummary.Trim()}");
        }

        sb.AppendLine("=======================");
        sb.AppendLine();
        sb.AppendLine("-------- MANDATORY PERSONALIZED OPENING GREETING --------");
        sb.AppendLine("LANGUAGE WARNING: this whole LEARNER CONTEXT block (including this instruction) is written in English purely for internal consistency — it has NOTHING to do with which language you must actually speak. ALWAYS obey the separate LANGUAGE hard rule given elsewhere in this system prompt (English-only or Spanish-only for the entire session) for every single word you say, including this opening greeting. Never let this English text pull you into English if the required language is Spanish, or vice versa.");
        sb.AppendLine("When the session STARTS, before beginning Step 1 (Hypothesis) or teaching, open with a SHORT personalized greeting (2-3 sentences max) proving you know the learner. It must:");
        sb.AppendLine("  1. Address the learner BY NAME.");
        sb.AppendLine("  2. State why THIS concept matters for them. If this is a STUDENT (see IMPORTANT note above), connect it to their degree/academic record instead — NEVER mention a job role, workplace objective, or KPI for them. Otherwise, only use their real objective, role gap, or impact tier IF it is genuinely, topically related to the Current Engram/Lesson Title above (e.g. don't claim a security-certification objective is served by a lesson about an unrelated topic just because both exist) — otherwise, connect it to their role or impact tier instead of forcing a fake link to an unrelated objective.");
        sb.AppendLine("  3. Briefly signal how you will adapt (e.g. 'I will keep it at a senior level since you have deep experience in your field').");
        sb.AppendLine("Example tone ONLY (do NOT hardcode the words; generate it naturally, in the REQUIRED language, from LEARNER CONTEXT; never name a specific programming language or technology stack unless it is truly relevant to the topic being taught):");
        sb.AppendLine("  \"Hi Alex. This concept is high-impact for you because it directly supports your Q4 OWASP security objective. Since you have years of experience, I'll get straight to the point. Let's begin.\"");
        sb.AppendLine("RULES for the greeting:");
        sb.AppendLine("  - Use ONLY real data from LEARNER CONTEXT. If a datum (name, objective, tools) is missing, omit it gracefully — never invent it.");
        sb.AppendLine("  - Keep it natural, conversational and warm, NOT a robotic checklist.");
        sb.AppendLine("  - Do NOT read raw numbers/scores aloud (say 'high impact', not '95').");
        sb.AppendLine("  - After the greeting, proceed normally into the lesson, respecting the exact 5-step curriculum without skipping or reordering.");
        sb.AppendLine("  - If NO learner context exists (fallback), give a normal generic greeting without personal claims.");
        sb.AppendLine("------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("-------- FIXED PERSONALIZATION INSTRUCTIONS --------");
        sb.AppendLine("You are a one-to-one tutor for the learner described in LEARNER CONTEXT. Everything below is written in English for internal consistency only — it does NOT change which language you speak; always follow the separate LANGUAGE hard rule elsewhere in this prompt.");
        sb.AppendLine("Adapt every step to this person:");
        sb.AppendLine("- In Hypothesis: frame the prediction question around their role, goal, or experience.");
        sb.AppendLine("- In Teaching: use examples with THEIR tools/technologies when possible, AND explicitly explain — in your own words, at least once during the class — how mastering THIS specific content (see \"Current Engram/Lesson Title\" above) helps them make progress on whichever of their real Active SMART Objectives are ACTUALLY topically related to it. Never force a connection to an objective that has nothing to do with this content — if none of their objectives are genuinely related, simply skip this and explain the content's value on its own merits instead.");
        sb.AppendLine("- In Deep Dive (card review/quiz): you MUST include a genuine, SPECIFIC connection between THIS card's actual concept and the learner's real role/objectives/tools — never skip personalization entirely, and never use a generic, interchangeable filler line (e.g. never just say something as vague as \"this will help you with your objectives\" without saying HOW). Say concretely, in one or two sentences, WHY this specific concept matters for their specific role/tools/objectives, using real details from LEARNER CONTEXT. Vary the wording and the angle card to card — never reuse the same templated sentence structure repeatedly across different cards. This personalization is a brief, genuine hook (not a substitute for the lesson): immediately after it, you MUST still fully and faithfully present the card's ENTIRE content — every concept, term, and detail written in it — exactly as the DEEP DIVE instructions below require.");
        sb.AppendLine("- In Recall & Production: connect the exercise to their active objective or KPI when relevant.");
        sb.AppendLine("- In Reflection & Review (Closing): If the learner provided an initial hypothesis ('Student Initial Hypothesis / Intuition' above), explicitly acknowledge how their intuition evolved (e.g. 'At first you thought X, but now you've confirmed Y...', said in the required language). This reinforces productive difficulty and solidifies the breakthrough.");
        sb.AppendLine("- Acknowledge their experience level (do not over-explain basics to a senior; do not assume knowledge a beginner lacks).");
        sb.AppendLine("- Tone (Balanced): Connect topics to their strategic goals/KPIs naturally at the beginning and during assessments, without repeating it on every sentence.");
        sb.AppendLine("RULES:");
        sb.AppendLine("- Use ONLY the facts in LEARNER CONTEXT. NEVER invent objectives, KPIs, coaching notes, or personal data not present.");
        sb.AppendLine("- Do NOT skip or reorder the 5 steps. Do NOT change the Factuals.");
        sb.AppendLine("- Do NOT reveal internal scores/numbers unless naturally motivating (you may say 'this is high-impact for your role', not 'score 95').");
        sb.AppendLine("- Personalization changes HOW you explain, never WHAT is true. It NEVER changes WHICH language you speak.");
        sb.AppendLine("----------------------------------------------------------");

        return sb.ToString();
    }
}