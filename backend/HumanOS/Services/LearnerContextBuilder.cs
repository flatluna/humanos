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
        CancellationToken cancellationToken = default);
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
        CancellationToken cancellationToken = default)
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

        // 1. Name & Role + Experience
        var name = !string.IsNullOrWhiteSpace(profile.EmployeeName) ? profile.EmployeeName.Trim() : "Learner";
        var experienceStr = profile.YearsOfExperience.HasValue ? $" ({profile.YearsOfExperience.Value}+ years of experience)" : string.Empty;
        var roleStr = !string.IsNullOrWhiteSpace(profile.RoleTitle) ? profile.RoleTitle.Trim() : "Professional";
        sb.AppendLine($"- Learner: {name}");
        sb.AppendLine($"- Role: {roleStr}{experienceStr}");

        if (!string.IsNullOrWhiteSpace(profile.RolePurpose))
        {
            sb.AppendLine($"- Role Mission: {profile.RolePurpose.Trim()}");
        }

        // 2. Tier, PrimarySignal, Reason for THIS Engram
        if (impactScore is not null)
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

        // 3. Active SMART Objectives
        if (profile.ActiveObjectives.Count > 0)
        {
            sb.AppendLine("- Active SMART Objectives:");
            foreach (var obj in profile.ActiveObjectives.Take(3))
            {
                var metricPart = !string.IsNullOrWhiteSpace(obj.Metric) ? $" [Target: {obj.Metric}]" : string.Empty;
                var quarterPart = !string.IsNullOrWhiteSpace(obj.Quarter) ? $" ({obj.Quarter})" : string.Empty;
                sb.AppendLine($"  * {obj.Title}{metricPart}{quarterPart}");
            }
        }

        // 4. Performance KPIs
        if (profile.Kpis.Count > 0)
        {
            sb.AppendLine("- Measured Role KPIs:");
            foreach (var kpi in profile.Kpis.Take(3))
            {
                sb.AppendLine($"  * {kpi}");
            }
        }

        // 5. Relevant Coaching Notes & Growth Areas
        if (profile.CoachingNotes.Count > 0)
        {
            sb.AppendLine("- Manager Coaching & Focus Areas:");
            foreach (var coach in profile.CoachingNotes.Take(3))
            {
                var competency = !string.IsNullOrWhiteSpace(coach.CompetencyName) ? coach.CompetencyName : coach.CompetencyType;
                var note = !string.IsNullOrWhiteSpace(coach.CoachingNote) ? $" — \"{coach.CoachingNote}\"" : string.Empty;
                sb.AppendLine($"  * {competency}{note}");
            }
        }

        // 6. Tools & Technologies (for concrete examples)
        var combinedTools = profile.Tools
            .Concat(profile.TechnicalSkills.Take(5))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (combinedTools.Count > 0)
        {
            sb.AppendLine($"- Known Tools & Technical Stack: {string.Join(", ", combinedTools.Take(8))}");
        }

        // 7. Gap between Role Demands vs Demonstrated Resume
        var roleDemands = profile.TechnicalSkills
            .Concat(profile.CoreResponsibilities.Take(3))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resumeDemonstrated = profile.ResumeSkills
            .Concat(profile.Certifications)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roleDemands.Count > 0 && resumeDemonstrated.Count > 0)
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
        else if (!string.IsNullOrWhiteSpace(profile.ResumeSummary))
        {
            sb.AppendLine($"- Background Summary: {profile.ResumeSummary.Trim()}");
        }

        sb.AppendLine("=======================");
        sb.AppendLine();
        sb.AppendLine("-------- SALUDO INICIAL PERSONALIZADO (obligatorio) --------");
        sb.AppendLine("When the session STARTS, before beginning Step 1 (Hypothesis) or teaching, open with a SHORT personalized greeting (2-3 sentences max) proving you know the learner. It must:");
        sb.AppendLine("  1. Address the learner BY NAME.");
        sb.AppendLine("  2. State why THIS concept matters for them, using their real objective, role gap, or impact tier (e.g. 'this supports your Q4 OWASP goal' or 'this is high-impact for your role').");
        sb.AppendLine("  3. Briefly signal how you will adapt (e.g. 'I will use C#/.NET examples and keep it at a senior level since you have deep backend experience').");
        sb.AppendLine("Example tone (do NOT hardcode; generate naturally from LEARNER CONTEXT):");
        sb.AppendLine("  \"Hola Alejandro. Este concepto es de alto impacto para ti porque apoya directamente tu objetivo de seguridad OWASP de este trimestre. Como tienes años de experiencia en backend, iré directo al grano y usaré ejemplos en C# y .NET. Empecemos.\"");
        sb.AppendLine("RULES for the greeting:");
        sb.AppendLine("  - Use ONLY real data from LEARNER CONTEXT. If a datum (name, objective, tools) is missing, omit it gracefully — never invent it.");
        sb.AppendLine("  - Keep it natural, conversational and warm, NOT a robotic checklist.");
        sb.AppendLine("  - Do NOT read raw numbers/scores aloud (say 'high impact', not '95').");
        sb.AppendLine("  - After the greeting, proceed normally into the lesson, respecting the exact 5-step curriculum without skipping or reordering.");
        sb.AppendLine("  - If NO learner context exists (fallback), give a normal generic greeting without personal claims.");
        sb.AppendLine("------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("-------- INSTRUCCIONES DE PERSONALIZACIÓN (fijas) --------");
        sb.AppendLine("You are a one-to-one tutor for the learner described in LEARNER CONTEXT.");
        sb.AppendLine("Adapt every step to this person:");
        sb.AppendLine("- In Hypothesis: frame the prediction question around their role, goal, or experience.");
        sb.AppendLine("- In Teaching: use examples with THEIR tools/technologies when possible.");
        sb.AppendLine("- In Recall & Production: connect the exercise to their active objective or KPI when relevant.");
        sb.AppendLine("- In Reflection & Review (Closing): If the learner provided an initial hypothesis ('Student Initial Hypothesis / Intuition' above), explicitly acknowledge how their intuition evolved (e.g. 'Al inicio pensabas que X, pero ahora comprobaste que Y...'). This reinforces productive difficulty and solidifies the breakthrough.");
        sb.AppendLine("- Acknowledge their experience level (do not over-explain basics to a senior; do not assume knowledge a beginner lacks).");
        sb.AppendLine("- Tone (Balanced): Connect topics to their strategic goals/KPIs naturally at the beginning and during assessments, without repeating it on every sentence.");
        sb.AppendLine("RULES:");
        sb.AppendLine("- Use ONLY the facts in LEARNER CONTEXT. NEVER invent objectives, KPIs, coaching notes, or personal data not present.");
        sb.AppendLine("- Do NOT skip or reorder the 5 steps. Do NOT change the Factuals.");
        sb.AppendLine("- Do NOT reveal internal scores/numbers unless naturally motivating (you may say 'this is high-impact for your role', not 'score 95').");
        sb.AppendLine("- Personalization changes HOW you explain, never WHAT is true.");
        sb.AppendLine("----------------------------------------------------------");

        return sb.ToString();
    }
}