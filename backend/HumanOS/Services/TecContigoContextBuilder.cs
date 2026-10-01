using System.Text;
using System.Text.Json;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Builds the grounded CONTEXT block for <see cref="HumanOS.Agents.TecContigoAgent"/>
/// ("tecBot" for students) — gathers ONLY real, already-persisted facts about one
/// student: their "Intereses" (PersonAcademicPreferences), profile/résumé, the
/// REAL course catalog (name + short description only, capped), and their own
/// enrolled capabilities with engram counts/status/average score. Read-only,
/// never fails/throws — degrades to a smaller context when data is missing.
/// </summary>
public sealed class TecContigoContextBuilder
{
    private readonly HumanOsDbContext _dbContext;
    private readonly PersonAcademicPreferencesService _academicPreferencesService;
    private readonly CapabilityEnrollmentService _capabilityEnrollmentService;
    private readonly PersonResumeService _personResumeService;
    private readonly BadgeService _badgeService;

    public TecContigoContextBuilder(
        HumanOsDbContext dbContext,
        PersonAcademicPreferencesService academicPreferencesService,
        CapabilityEnrollmentService capabilityEnrollmentService,
        PersonResumeService personResumeService,
        BadgeService badgeService)
    {
        _dbContext = dbContext;
        _academicPreferencesService = academicPreferencesService;
        _capabilityEnrollmentService = capabilityEnrollmentService;
        _personResumeService = personResumeService;
        _badgeService = badgeService;
    }

    public async Task<string> BuildAsync(Guid? personId, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== TEC CONTIGO STAGE ===");
        sb.AppendLine("Mientras estudias (current stage) — completing the academic stage, strengthening knowledge, preparing for the next opportunity.");

        if (personId.HasValue && personId.Value != Guid.Empty)
        {
            await AppendStudentSectionAsync(sb, personId.Value, cancellationToken);
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("=== STUDENT ===");
            sb.AppendLine("(No student identity resolved — offer only general orientation.)");
        }

        await AppendCatalogAsync(sb, cancellationToken);

        return sb.ToString();
    }

    private async Task AppendStudentSectionAsync(StringBuilder sb, Guid personId, CancellationToken cancellationToken)
    {
        sb.AppendLine();
        sb.AppendLine("=== STUDENT INTERESTS (from 'Intereses') ===");
        try
        {
            var preferences = await _academicPreferencesService.GetAsync(personId, cancellationToken);
            if (preferences is null)
            {
                sb.AppendLine("(No interests saved yet.)");
            }
            else
            {
                AppendList(sb, "Academic interests", preferences.AcademicInterestsJson);
                AppendList(sb, "Learning preferences", preferences.LearningPreferencesJson);
                AppendList(sb, "Academic motivations", preferences.AcademicMotivationsJson);
                AppendList(sb, "Growth areas", preferences.GrowthAreasJson);
                if (!string.IsNullOrWhiteSpace(preferences.Frustrations)) sb.AppendLine($"- Frustrations: {preferences.Frustrations}");
                if (!string.IsNullOrWhiteSpace(preferences.MainChallenges)) sb.AppendLine($"- Main challenges: {preferences.MainChallenges}");
                if (!string.IsNullOrWhiteSpace(preferences.ImportantValues)) sb.AppendLine($"- Important values: {preferences.ImportantValues}");
            }
        }
        catch
        {
            sb.AppendLine("(Interests unavailable right now.)");
        }

        sb.AppendLine();
        sb.AppendLine("=== STUDENT PROFILE ===");
        try
        {
            var resume = await _personResumeService.GetByPersonIdAsync(personId, cancellationToken);
            var hasAnything = !string.IsNullOrWhiteSpace(resume.ProfessionalObjective)
                || !string.IsNullOrWhiteSpace(resume.ProfessionalSummary)
                || resume.Skills.Count > 0
                || resume.Education.Count > 0;

            if (!hasAnything)
            {
                sb.AppendLine("(No résumé/profile on file.)");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(resume.ProfessionalObjective)) sb.AppendLine($"- Professional objective: {resume.ProfessionalObjective}");
                if (!string.IsNullOrWhiteSpace(resume.ProfessionalSummary)) sb.AppendLine($"- Summary: {resume.ProfessionalSummary}");
                if (resume.Skills.Count > 0) sb.AppendLine($"- Skills: {string.Join(", ", resume.Skills.Take(15))}");
                if (resume.Education.Count > 0) sb.AppendLine($"- Education: {string.Join("; ", resume.Education.Take(5))}");

                AppendAcademicRecord(sb, resume.StudentAcademicProfile ?? new HumanOS.Contracts.People.StudentAcademicProfile());
            }
        }
        catch
        {
            sb.AppendLine("(Profile unavailable right now.)");
        }

        sb.AppendLine();
        sb.AppendLine("=== HUMAN OS / ENGRAMS PROGRESS (separate from Tec academic grades) ===");
        sb.AppendLine("An \"Engram\" is one small, named learning unit (a subchapter/section, e.g. \"1.1 What Is Artificial Intelligence?\") inside a Human OS course. Each Engram has its own mastery score (0-100%) from the student's real Assessment attempts — it is NOT a university grade.");
        try
        {
            var enrolled = await _capabilityEnrollmentService.GetEnrolledCapabilitiesAsync(personId, cancellationToken);
            if (enrolled.Count == 0)
            {
                sb.AppendLine("(No enrolled courses yet.)");
            }
            else
            {
                foreach (var course in enrolled)
                {
                    var scoreText = course.AverageScore.HasValue ? $"{course.AverageScore.Value}/100" : "no grade yet";
                    sb.AppendLine(
                        $"- \"{course.Name}\" — Status: {course.Status}; Engrams: {course.MasteredNodes}/{course.TotalNodes} mastered; Average score: {scoreText}");
                }
            }

            await AppendEngramDetailAsync(sb, personId, cancellationToken);
            await AppendAssessmentEvidenceAsync(sb, personId, cancellationToken);
            await AppendGamificationAsync(sb, personId, cancellationToken);
        }
        catch
        {
            sb.AppendLine("(Enrolled courses unavailable right now.)");
        }
    }

    /// <summary>Per-Engram (per-subchapter-section) breakdown: course name, Engram title,
    /// latest attempt's score/100 and how long ago it was completed — the same detail level
    /// shown in the student's own "Brain"/course progress screens (e.g. "1.1 What Is
    /// Artificial Intelligence? — 88% — hace 10 días"), so tecBot can talk about individual
    /// Engrams by name, not just course-level averages.</summary>
    private async Task AppendEngramDetailAsync(StringBuilder sb, Guid personId, CancellationToken cancellationToken)
    {
        sb.AppendLine();
        sb.AppendLine("=== ENGRAM-BY-ENGRAM DETAIL (each Engram's own name and score) ===");

        // Loaded as flat rows first (EF Core can't reliably translate an
        // OrderBy().First() inside a GroupBy().Select() for this provider) —
        // the "latest round per section" reduction happens in memory instead,
        // which is fine given this is scoped to a single student.
        var allRounds = await _dbContext.StructuredLearningAssessmentRounds
            .AsNoTracking()
            .Where(round => round.PersonId == personId)
            .Select(round => new
            {
                round.StructuredLearningDocumentSectionId,
                CourseName = round.Section.Document.DocumentName,
                EngramTitle = round.Section.SubchapterTitle,
                round.Section.SortOrder,
                round.FinalScore,
                Status = round.Status.ToString(),
                round.CompletedDate,
                round.CreatedDate,
            })
            .ToListAsync(cancellationToken);

        var latestRoundsPerSection = allRounds
            .GroupBy(round => round.StructuredLearningDocumentSectionId)
            .Select(group => group.OrderByDescending(round => round.CreatedDate).First())
            .Select(round => new
            {
                round.CourseName,
                round.EngramTitle,
                round.SortOrder,
                round.FinalScore,
                round.Status,
                CompletedDate = round.CompletedDate ?? round.CreatedDate,
            })
            .ToList();

        if (latestRoundsPerSection.Count == 0)
        {
            sb.AppendLine("(No Engram attempts are recorded yet.)");
            return;
        }

        foreach (var group in latestRoundsPerSection
            .GroupBy(row => row.CourseName)
            .OrderBy(group => group.Key))
        {
            sb.AppendLine($"- Course \"{group.Key}\":");
            foreach (var engram in group.OrderBy(row => row.SortOrder))
            {
                var scoreText = engram.FinalScore.HasValue ? $"{engram.FinalScore.Value}%" : "not scored yet";
                var daysAgo = Math.Max(0, (int)(DateTime.UtcNow - engram.CompletedDate).TotalDays);
                sb.AppendLine($"  * Engram \"{engram.EngramTitle}\" — {scoreText}; Status: {engram.Status}; {daysAgo} day(s) ago.");
            }
        }
    }

    /// <summary>Human OS gamification (levels, streaks, and badge/medal progress) — real
    /// facts from PlayerProfiles/BadgeService, never invented. Explains WHAT each badge
    /// means (its rule), never just an emoji/name, so tecBot can answer "what does this
    /// achievement mean" and "how close am I to unlocking X" accurately.</summary>
    private async Task AppendGamificationAsync(StringBuilder sb, Guid personId, CancellationToken cancellationToken)
    {
        sb.AppendLine();
        sb.AppendLine("=== HUMAN OS GAMIFICATION (levels, streaks, badges/medals — separate from grades) ===");
        try
        {
            var profile = await _dbContext.PlayerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PersonId == personId, cancellationToken);

            if (profile is not null)
            {
                sb.AppendLine(
                    $"- Level {profile.CurrentLevel} (\"{profile.CurrentLevelName}\"), {profile.TotalPoints} total points, " +
                    $"current streak {profile.CurrentStreakDays} day(s) (longest {profile.LongestStreakDays}), " +
                    $"{profile.TotalEngramsMastered} Engrams mastered overall.");
            }

            var badgeProgress = await _badgeService.GetBadgeProgressAsync(personId, cancellationToken);
            var earned = badgeProgress.Where(b => b.IsEarned).ToList();
            var inProgress = badgeProgress.Where(b => !b.IsEarned && b.ProgressPercent > 0).OrderByDescending(b => b.ProgressPercent).ToList();

            if (earned.Count > 0)
            {
                sb.AppendLine($"- Unlocked medals ({earned.Count}):");
                foreach (var badge in earned)
                {
                    sb.AppendLine($"  * \"{badge.Name}\" ({badge.IconEmoji}) — {badge.Description} — unlocked {badge.EarnedAt:yyyy-MM-dd}.");
                }
            }

            if (inProgress.Count > 0)
            {
                sb.AppendLine("- Closest medals to unlock:");
                foreach (var badge in inProgress.Take(6))
                {
                    sb.AppendLine($"  * \"{badge.Name}\" ({badge.IconEmoji}) — {badge.Description} — progress {badge.CurrentValue}/{badge.TargetValue} ({badge.ProgressPercent}%).");
                }
            }

            if (earned.Count == 0 && inProgress.Count == 0)
            {
                sb.AppendLine("(No gamification/badge data yet.)");
            }
        }
        catch
        {
            sb.AppendLine("(Gamification data unavailable right now.)");
        }
    }

    private static void AppendAcademicRecord(StringBuilder sb, HumanOS.Contracts.People.StudentAcademicProfile academic)
    {
        var hasRecord = !string.IsNullOrWhiteSpace(academic.Institution)
            || !string.IsNullOrWhiteSpace(academic.Degree)
            || academic.Semesters.Count > 0;

        if (!hasRecord) return;

        sb.AppendLine();
        sb.AppendLine("=== TEC ACADEMIC RECORD (official academic grades; NEVER mix with Human OS/Engrams scores) ===");
        if (!string.IsNullOrWhiteSpace(academic.Institution)) sb.AppendLine($"- Institution: {academic.Institution}");
        if (!string.IsNullOrWhiteSpace(academic.Degree)) sb.AppendLine($"- Degree: {academic.Degree}");
        if (!string.IsNullOrWhiteSpace(academic.AcademicStage)) sb.AppendLine($"- Academic stage: {academic.AcademicStage}");
        if (academic.CumulativeAverage is not null)
        {
            sb.AppendLine($"- Cumulative academic average: {academic.CumulativeAverage}/{academic.AverageScale ?? "10"}");
        }

        foreach (var semester in academic.Semesters)
        {
            var semesterAverage = semester.Average is null ? "In progress" : $"{semester.Average}/{academic.AverageScale ?? "10"}";
            sb.AppendLine($"- {semester.Name ?? "Semester"}: {semesterAverage}");
            foreach (var subject in semester.Subjects)
            {
                var grade = subject.Grade is null ? "In progress" : $"{subject.Grade}/{academic.AverageScale ?? "10"}";
                sb.AppendLine($"  * {subject.Name ?? "Unnamed subject"}: {grade}");
            }
        }
    }

    private async Task AppendAssessmentEvidenceAsync(StringBuilder sb, Guid personId, CancellationToken cancellationToken)
    {
        sb.AppendLine();
        sb.AppendLine("=== COMPLETE ASSESSMENT HISTORY (real grades, answers and failure reasons) ===");

        var attempts = await _dbContext.StructuredLearningAssessmentQuestionAttempts
            .AsNoTracking()
            .Where(question => question.Round.PersonId == personId && question.StudentAnswer != null)
            .OrderByDescending(question => question.AnsweredDate ?? question.CreatedDate)
            .Take(40)
            .Select(question => new
            {
                Course = question.Round.Section.Document.DocumentName,
                Section = question.Round.Section.ChapterTitle,
                question.QuestionText,
                question.StudentAnswer,
                question.ScoreContribution,
                Correctness = question.Correctness.ToString(),
                question.Feedback,
                question.ObservedError,
                RoundScore = question.Round.FinalScore,
                RoundStatus = question.Round.Status.ToString(),
                AnsweredDate = question.AnsweredDate ?? question.CreatedDate,
            })
            .ToListAsync(cancellationToken);

        if (attempts.Count == 0)
        {
            sb.AppendLine("(No answered assessment questions are recorded yet.)");
            return;
        }

        foreach (var attempt in attempts)
        {
            sb.AppendLine($"- Course: {attempt.Course}; Section: {attempt.Section ?? "Unspecified"}; Date: {attempt.AnsweredDate:yyyy-MM-dd}");
            sb.AppendLine($"  Question: {attempt.QuestionText}");
            sb.AppendLine($"  Student answer: {attempt.StudentAnswer}");
            sb.AppendLine($"  Score: {attempt.ScoreContribution?.ToString() ?? "not graded"}/100; Correctness: {attempt.Correctness}; Round: {attempt.RoundStatus} ({attempt.RoundScore?.ToString() ?? "not finalized"}/100)");
            if (!string.IsNullOrWhiteSpace(attempt.ObservedError)) sb.AppendLine($"  Observed error: {attempt.ObservedError}");
            if (!string.IsNullOrWhiteSpace(attempt.Feedback)) sb.AppendLine($"  Evaluator feedback: {attempt.Feedback}");
        }
    }

    private async Task AppendCatalogAsync(StringBuilder sb, CancellationToken cancellationToken)
    {
        sb.AppendLine();
        sb.AppendLine("=== FULL COURSE CATALOG (name + short description — the ONLY courses you may recommend by name) ===");
        try
        {
            var structuredLearningCourses = await _dbContext.StructuredLearningDocuments
                .AsNoTracking()
                .Where(d => d.IsPublished && d.IsGlobal)
                .OrderBy(d => d.DocumentName)
                .Select(d => new { d.DocumentName, d.ExecutiveSummary })
                .Take(150)
                .ToListAsync(cancellationToken);

            foreach (var course in structuredLearningCourses)
            {
                var shortDescription = Truncate(course.ExecutiveSummary, 140);
                sb.AppendLine($"- {course.DocumentName}: {shortDescription}");
            }

            var capabilities = await _dbContext.Capabilities
                .AsNoTracking()
                .Where(c => c.IsActive && c.OwnerPersonId == null && c.OwnerTenantId == null)
                .OrderBy(c => c.Name)
                .Select(c => new { c.Name, c.Description, c.ExecutiveSummary })
                .Take(150)
                .ToListAsync(cancellationToken);

            foreach (var capability in capabilities)
            {
                var shortDescription = Truncate(capability.ExecutiveSummary ?? capability.Description, 140);
                sb.AppendLine($"- {capability.Name}: {shortDescription}");
            }

            if (structuredLearningCourses.Count == 0 && capabilities.Count == 0)
            {
                sb.AppendLine("(Catalog is currently empty.)");
            }
        }
        catch
        {
            sb.AppendLine("(Catalog unavailable right now.)");
        }
    }

    private static void AppendList(StringBuilder sb, string label, string json)
    {
        var values = DeserializeStringList(json);
        if (values.Count > 0)
        {
            sb.AppendLine($"- {label}: {string.Join(", ", values)}");
        }
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text)) return "(no description)";
        var trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].TrimEnd() + "...";
    }
}
