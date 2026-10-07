using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Studio;
using HumanOS.Contracts.Tenancy;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>
/// Read-only reporting over data already captured by
/// <see cref="StructuredLearningEngramSessionService"/> (session/step
/// timing), the Recall/Assessment engines (attempts/rounds), and
/// <see cref="StructuredLearningEngramUsageService"/> (tokens) — no new
/// capture, just aggregation (2026-10-02). Answers: how long a student
/// spent in a session and in each step, how many Recall attempts
/// failed/mastered, Assessment pass/fail counts, and token usage — per
/// real session, or rolled up for an engram across every session/person.
/// </summary>
public sealed class StructuredLearningEngramStatsService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public StructuredLearningEngramStatsService(HumanOsDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public sealed class StepStat
    {
        public Guid SessionId { get; set; }
        public Guid? StructuredLearningDocumentSectionId { get; set; }
        public string StepType { get; set; } = string.Empty;
        public DateTime StartedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public int DurationSeconds { get; set; }
        public long DurationMilliseconds { get; set; }
        public bool IsEstimated { get; set; }
    }

    public sealed class RecallStat
    {
        public int AttemptsCount { get; set; }
        public int MasteredCount { get; set; }
        public int FailedCount { get; set; }
        public double? AverageScore { get; set; }
    }

    public sealed class AssessmentStat
    {
        public int RoundsCount { get; set; }
        public int PassedRounds { get; set; }
        public int FailedRounds { get; set; }
        public int QuestionsCount { get; set; }
        public int IncorrectAnswers { get; set; }
    }

    public sealed class UsageStat
    {
        public string Channel { get; set; } = string.Empty;
        public string? ModelName { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int DurationSeconds { get; set; }
    }

    public sealed class UsageCostStat
    {
        public string StepType { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public int CallCount { get; set; }
        public int InputTokens { get; set; }
        public decimal InputRatePerMillionTokens { get; set; }
        public decimal InputCostUsd { get; set; }
        public int OutputTokens { get; set; }
        public decimal OutputRatePerMillionTokens { get; set; }
        public decimal OutputCostUsd { get; set; }
        public decimal TotalCostUsd { get; set; }
    }

    /// <summary>Combined time + token view for ONE of the 6 pedagogy steps
    /// (2026-10-02) — merges StructuredLearningEngramSessionStep timing with
    /// StructuredLearningEngramRuntimeUsage tokens (now tagged with StepType),
    /// split by Voice/Text channel since Teaching and DeepDive support both.</summary>
    public sealed class StepSummary
    {
        public string StepType { get; set; } = string.Empty;
        public int SessionsCount { get; set; }
        public int TotalDurationSeconds { get; set; }
        public int AverageDurationSeconds { get; set; }
        public bool HasEstimatedTiming { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int VoiceInputTokens { get; set; }
        public int VoiceOutputTokens { get; set; }
        public int TextInputTokens { get; set; }
        public int TextOutputTokens { get; set; }
        public string ModelNames { get; set; } = string.Empty;
        public decimal EstimatedCostUsd { get; set; }
    }

    public sealed class SessionStatsResult
    {
        public Guid StructuredLearningEngramSessionId { get; set; }
        public DateTime StartedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public int DurationSeconds { get; set; }
        public List<StepStat> Steps { get; set; } = [];
        public RecallStat Recall { get; set; } = new();
        public AssessmentStat Assessment { get; set; } = new();
        public List<UsageStat> Usage { get; set; } = [];
    }

    public sealed class EngramStatsResult
    {
        public Guid StructuredLearningDocumentSectionId { get; set; }
        public int TotalSessionsVisited { get; set; }
        public int TotalTimeSeconds { get; set; }
        public int VoiceSessionCount { get; set; }
        public int AverageTimeSecondsPerVisit { get; set; }
        public int TotalInputTokens { get; set; }
        public int TotalOutputTokens { get; set; }
        public decimal EstimatedCostUsd { get; set; }
        public long TextToSpeechCharacterCount { get; set; }
        public decimal TextToSpeechEstimatedCostUsd { get; set; }
        public int UnassignedInputTokens { get; set; }
        public int UnassignedOutputTokens { get; set; }
        public List<StepStat> StepBreakdown { get; set; } = [];
        public List<StepSummary> StepSummaries { get; set; } = [];
        public RecallStat Recall { get; set; } = new();
        public AssessmentStat Assessment { get; set; } = new();
        public List<UsageStat> Usage { get; set; } = [];
        public List<UsageCostStat> UsageCostBreakdown { get; set; } = [];
    }

    /// <summary>One row per course (document) this person has any enrollment
    /// or activity in — powers the per-user learning dashboard's course
    /// breakdown charts (2026-10-03).</summary>
    public sealed class PersonCourseDashboardRow
    {
        public Guid StructuredLearningDocumentId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public int SectionCount { get; set; }
        public int CompletedSectionCount { get; set; }
        public bool IsCompleted { get; set; }
        public int TotalTimeSeconds { get; set; }
        public int TotalInputTokens { get; set; }
        public int TotalOutputTokens { get; set; }
        public long TextToSpeechCharacterCount { get; set; }
        public decimal TextToSpeechEstimatedCostUsd { get; set; }
        public decimal EstimatedCostUsd { get; set; }
    }

    /// <summary>One "Reactivar Engrama" (Brain spaced-repetition review)
    /// node this person has ever reviewed — real duration/cost rolled up
    /// across every session recorded in <see cref="Models.Learning.EngramReviewSessionUsage"/>
    /// (2026-10-04). Cost is Realtime-voice-per-minute only (this feature
    /// is voice-only, no separate token ledger exists for it yet).</summary>
    public sealed class PersonEngramReviewNodeRow
    {
        public Guid CapabilityGraphNodeId { get; set; }
        public string NodeName { get; set; } = string.Empty;
        public int SessionCount { get; set; }
        public int TotalDurationSeconds { get; set; }
        public decimal EstimatedCostUsd { get; set; }
    }

    /// <summary>Real "Reactivar Engrama" spend summary for ONE person
    /// (2026-10-04) — totals plus a per-node breakdown. Separate domain
    /// from <see cref="PersonLearningDashboardResult"/> (that one covers
    /// in-course Learn/Recall/Assessment runtime; this one covers Brain's
    /// standalone spaced-repetition review feature).</summary>
    public sealed class PersonEngramReviewUsageResult
    {
        public int TotalSessions { get; set; }
        public int TotalDurationSeconds { get; set; }
        public decimal EstimatedCostUsd { get; set; }
        public List<PersonEngramReviewNodeRow> Nodes { get; set; } = [];
    }

    /// <summary>Full cross-course learning dashboard for ONE person
    /// (2026-10-03) — courses completed/in progress, engrams mastered,
    /// token/cost/time totals (courses + Buddy voice usage), Recall/
    /// Assessment pass rates, and a per-course breakdown for bar/pie
    /// charts. Powers the admin "Users" person detail page's "Dashboard"
    /// tab. <see cref="TotalEstimatedCostUsd"/> and <see cref="TotalTimeSeconds"/>
    /// are the grand totals (courses + Buddy); <see cref="CourseEstimatedCostUsd"/>
    /// is courses only, matching the sum of <see cref="Courses"/>' own costs.</summary>
    public sealed class PersonLearningDashboardResult
    {
        public int CoursesCompleted { get; set; }
        public int CoursesInProgress { get; set; }
        public int TotalEngramsCompleted { get; set; }
        public int TotalEngramsAvailable { get; set; }
        public int TotalInputTokens { get; set; }
        public int TotalOutputTokens { get; set; }

        /// <summary>Grand total: course token costs + Buddy voice cost.</summary>
        public decimal TotalEstimatedCostUsd { get; set; }

        /// <summary>Course (engram) token costs only — matches the sum of
        /// <see cref="Courses"/>' own <see cref="PersonCourseDashboardRow.EstimatedCostUsd"/>.</summary>
        public decimal CourseEstimatedCostUsd { get; set; }

        /// <summary>Grand total: course time + Buddy conversation time.</summary>
        public int TotalTimeSeconds { get; set; }

        public RecallStat Recall { get; set; } = new();
        public AssessmentStat Assessment { get; set; } = new();
        public List<PersonCourseDashboardRow> Courses { get; set; } = [];

        /// <summary>This person's own Workplace Companion (Buddy) voice
        /// usage — session count, duration, and estimated cost.</summary>
        public TenantBuddyUsageResponse Buddy { get; set; } = new();
    }

    /// <summary>One row per engram (section) for the tenant-wide admin
    /// dashboard (2026-10-02) — same categories as <see cref="GetEngramStatsAsync"/>
    /// plus identifying labels (course/chapter/engram names) and a rolled-up
    /// estimated cost, so every engram across every course the tenant owns
    /// can be listed/sorted at once.</summary>
    public sealed class TenantEngramStatRow
    {
        public Guid StructuredLearningDocumentId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public Guid StructuredLearningDocumentSectionId { get; set; }
        public string ChapterTitle { get; set; } = string.Empty;
        public string SubchapterTitle { get; set; } = string.Empty;
        public int TotalSessionsVisited { get; set; }
        public int TotalTimeSeconds { get; set; }
        public RecallStat Recall { get; set; } = new();
        public AssessmentStat Assessment { get; set; } = new();
        public List<UsageStat> Usage { get; set; } = [];
        public int TotalInputTokens { get; set; }
        public int TotalOutputTokens { get; set; }
        public int VoiceSessionCount { get; set; }
        public long TextToSpeechCharacterCount { get; set; }
        public decimal TextToSpeechEstimatedCostUsd { get; set; }
        public decimal EstimatedCostUsd { get; set; }
    }

    /// <summary>Aggregate "courses taken" totals across EVERY engram this
    /// tenant's employees have ever visited (2026-10-06) — rolled up from
    /// <see cref="TenantEngramStatRow"/> so the tenant admin dashboard can
    /// show ONE "Cursos tomados" total (sessions, tokens, realtime voice,
    /// Recall/Assessment pass-fail, estimated cost) instead of only the
    /// per-engram list. Mirrors <see cref="HumanOS.Contracts.Tenancy.TenantCourseTotalsResponse"/>
    /// which covers the "Cursos generados" (creation) side.</summary>
    public sealed class TenantCourseConsumptionTotalsResult
    {
        public int CourseCount { get; set; }
        public int EngramsVisitedCount { get; set; }
        public int SessionsCount { get; set; }
        public int VoiceSessionCount { get; set; }
        public int TotalTimeSeconds { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long TotalTokens => InputTokens + OutputTokens;
        public long TextToSpeechCharacterCount { get; set; }
        public decimal TextToSpeechEstimatedCostUsd { get; set; }
        public decimal EstimatedCostUsd { get; set; }
        public RecallStat Recall { get; set; } = new();
        public AssessmentStat Assessment { get; set; } = new();
        public List<UsageStat> UsageByChannel { get; set; } = [];
    }

    /// <summary>Sums <see cref="GetTenantEngramStatsAsync"/> into a single
    /// tenant-wide "courses taken" total (2026-10-06) — powers the Courses
    /// tab's "Cursos tomados" totals section.</summary>
    public async Task<TenantCourseConsumptionTotalsResult> GetTenantCourseConsumptionTotalsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await GetTenantEngramStatsAsync(tenantId, cancellationToken);

        var result = new TenantCourseConsumptionTotalsResult
        {
            CourseCount = rows.Select(r => r.StructuredLearningDocumentId).Distinct().Count(),
            EngramsVisitedCount = rows.Count,
            SessionsCount = rows.Sum(r => r.TotalSessionsVisited),
            VoiceSessionCount = rows.Sum(r => r.VoiceSessionCount),
            TotalTimeSeconds = rows.Sum(r => r.TotalTimeSeconds),
            InputTokens = rows.Sum(r => (long)r.TotalInputTokens),
            OutputTokens = rows.Sum(r => (long)r.TotalOutputTokens),
            TextToSpeechCharacterCount = rows.Sum(r => r.TextToSpeechCharacterCount),
            TextToSpeechEstimatedCostUsd = Math.Round(rows.Sum(r => r.TextToSpeechEstimatedCostUsd), 4),
            EstimatedCostUsd = Math.Round(rows.Sum(r => r.EstimatedCostUsd), 4),
            Recall = new RecallStat
            {
                AttemptsCount = rows.Sum(r => r.Recall.AttemptsCount),
                MasteredCount = rows.Sum(r => r.Recall.MasteredCount),
                FailedCount = rows.Sum(r => r.Recall.FailedCount),
            },
            Assessment = new AssessmentStat
            {
                RoundsCount = rows.Sum(r => r.Assessment.RoundsCount),
                PassedRounds = rows.Sum(r => r.Assessment.PassedRounds),
                FailedRounds = rows.Sum(r => r.Assessment.FailedRounds),
                QuestionsCount = rows.Sum(r => r.Assessment.QuestionsCount),
                IncorrectAnswers = rows.Sum(r => r.Assessment.IncorrectAnswers),
            },
            UsageByChannel = rows
                .SelectMany(r => r.Usage)
                .GroupBy(u => new { u.Channel, u.ModelName })
                .Select(g => new UsageStat
                {
                    Channel = g.Key.Channel,
                    ModelName = g.Key.ModelName,
                    InputTokens = g.Sum(u => u.InputTokens),
                    OutputTokens = g.Sum(u => u.OutputTokens),
                    DurationSeconds = g.Sum(u => u.DurationSeconds),
                })
                .OrderByDescending(u => u.InputTokens + u.OutputTokens)
                .ToList(),
        };

        return result;
    }

    /// <summary>Every engram touched by any of this tenant's own employees
    /// (not by course ownership — most Structured Learning courses are
    /// global/shared, same convention as <see cref="TenantBuddyUsageService"/>'s
    /// person-tenant join), with session/step time, Recall/Assessment
    /// pass-fail counts, and token usage — powers the tenant admin
    /// dashboard's "Engrams" tab.</summary>
    public async Task<List<TenantEngramStatRow>> GetTenantEngramStatsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var personIds = await _dbContext.People
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.PersonId)
            .ToListAsync(cancellationToken);

        if (personIds.Count == 0) return [];

        var stepRows = await (
            from step in _dbContext.StructuredLearningEngramSessionSteps.AsNoTracking()
            join session in _dbContext.StructuredLearningEngramSessions.AsNoTracking()
                on step.StructuredLearningEngramSessionId equals session.StructuredLearningEngramSessionId
            where session.PersonId != null && personIds.Contains(session.PersonId.Value) && step.StructuredLearningDocumentSectionId != null
            select step
        ).ToListAsync(cancellationToken);

        var stepEndBoundsForTenant = await GetStepEndBoundsAsync(stepRows, cancellationToken);

        var recallRows = await _dbContext.StructuredLearningRecallAttempts
            .AsNoTracking()
            .Where(a => a.PersonId != null && personIds.Contains(a.PersonId.Value))
            .Select(a => new { a.StructuredLearningDocumentSectionId, a.RecallScore })
            .ToListAsync(cancellationToken);

        var roundRows = await _dbContext.StructuredLearningAssessmentRounds
            .AsNoTracking()
            .Include(r => r.Questions)
            .Where(r => r.PersonId != null && personIds.Contains(r.PersonId.Value))
            .ToListAsync(cancellationToken);

        var usageRows = await _dbContext.StructuredLearningEngramRuntimeUsages
            .AsNoTracking()
            .Where(u => u.PersonId != null && personIds.Contains(u.PersonId.Value) && u.StructuredLearningDocumentSectionId != null)
            .ToListAsync(cancellationToken);

        var sectionIds = stepRows.Select(s => s.StructuredLearningDocumentSectionId!.Value)
            .Concat(recallRows.Select(a => a.StructuredLearningDocumentSectionId))
            .Concat(roundRows.Select(r => r.StructuredLearningDocumentSectionId))
            .Concat(usageRows.Select(u => u.StructuredLearningDocumentSectionId!.Value))
            .Distinct()
            .ToList();

        if (sectionIds.Count == 0) return [];

        var sections = await _dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Where(s => sectionIds.Contains(s.StructuredLearningDocumentSectionId))
            .Select(s => new { s.StructuredLearningDocumentSectionId, s.StructuredLearningDocumentId, s.ChapterTitle, s.SubchapterTitle })
            .ToListAsync(cancellationToken);

        var documentIds = sections.Select(s => s.StructuredLearningDocumentId).Distinct().ToList();
        var documentNames = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .Where(d => documentIds.Contains(d.StructuredLearningDocumentId))
            .Select(d => new { d.StructuredLearningDocumentId, d.DocumentName })
            .ToDictionaryAsync(d => d.StructuredLearningDocumentId, d => d.DocumentName, cancellationToken);

        var result = new List<TenantEngramStatRow>();
        foreach (var section in sections)
        {
            var sectionId = section.StructuredLearningDocumentSectionId;
            var stepsForSection = stepRows.Where(s => s.StructuredLearningDocumentSectionId == sectionId).ToList();
            var stepStats = BuildStepStats(stepsForSection, stepEndBoundsForTenant);
            var recallScores = recallRows.Where(a => a.StructuredLearningDocumentSectionId == sectionId).Select(a => a.RecallScore).ToList();
            var rounds = roundRows.Where(r => r.StructuredLearningDocumentSectionId == sectionId).ToList();
            var usage = usageRows.Where(u => u.StructuredLearningDocumentSectionId == sectionId).ToList();
            var usageStats = BuildUsageStats(usage);

            result.Add(new TenantEngramStatRow
            {
                StructuredLearningDocumentId = section.StructuredLearningDocumentId,
                DocumentName = documentNames.GetValueOrDefault(section.StructuredLearningDocumentId, string.Empty),
                StructuredLearningDocumentSectionId = sectionId,
                ChapterTitle = section.ChapterTitle,
                SubchapterTitle = section.SubchapterTitle,
                TotalSessionsVisited = stepsForSection.Select(s => s.StructuredLearningEngramSessionId).Distinct().Count(),
                TotalTimeSeconds = (int)(stepStats.Sum(s => s.DurationMilliseconds) / 1000),
                Recall = BuildRecallStat(recallScores),
                Assessment = BuildAssessmentStat(rounds),
                Usage = usageStats,
                TotalInputTokens = usageStats.Sum(u => u.InputTokens),
                TotalOutputTokens = usageStats.Sum(u => u.OutputTokens),
                VoiceSessionCount = usage.Count(u => u.Channel == "Voice" && u.DurationSeconds > 0),
                TextToSpeechCharacterCount = usage.Where(u => u.Channel == "TextToSpeech").Sum(u => (long)u.CharacterCount),
                TextToSpeechEstimatedCostUsd = EstimateTextToSpeechCost(usage),
                EstimatedCostUsd = EstimateUsageCost(usage),
            });
        }

        return result;
    }

    /// <summary>One row per engram (section) touched by a single person,
    /// across every course they have activity in — same richness as
    /// <see cref="GetEngramStatsAsync(Guid, Guid?, CancellationToken)"/>
    /// (VoiceSessionCount, UsageCostBreakdown, TextToSpeech) but computed
    /// in ONE batched set of queries instead of looping per-section per-
    /// course (2026-10-03). Powers the admin "Brain" tab's per-course,
    /// per-engram breakdown without the N+1 fetch the frontend used to do
    /// (one document fetch + one stats fetch per section, per course).</summary>
    public sealed class PersonEngramStatRow
    {
        public Guid StructuredLearningDocumentId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public Guid StructuredLearningDocumentSectionId { get; set; }
        public string ChapterTitle { get; set; } = string.Empty;
        public string SubchapterTitle { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int TotalSessionsVisited { get; set; }
        public int TotalTimeSeconds { get; set; }
        public int VoiceSessionCount { get; set; }
        public RecallStat Recall { get; set; } = new();
        public AssessmentStat Assessment { get; set; } = new();
        public List<UsageStat> Usage { get; set; } = [];
        public List<UsageCostStat> UsageCostBreakdown { get; set; } = [];
        public int TotalInputTokens { get; set; }
        public int TotalOutputTokens { get; set; }
        public long TextToSpeechCharacterCount { get; set; }
        public decimal TextToSpeechEstimatedCostUsd { get; set; }
    }

    /// <summary>Every engram this one person has any activity on, across
    /// every course — see <see cref="PersonEngramStatRow"/>. Powers the
    /// admin Person Detail page's "Brain" tab: a cheap single round-trip
    /// instead of fetching each enrolled course's document then each of
    /// its sections' stats one at a time.</summary>
    public async Task<List<PersonEngramStatRow>> GetPersonEngramStatsAsync(Guid personId, CancellationToken cancellationToken)
    {
        var stepRows = await (
            from step in _dbContext.StructuredLearningEngramSessionSteps.AsNoTracking()
            join session in _dbContext.StructuredLearningEngramSessions.AsNoTracking()
                on step.StructuredLearningEngramSessionId equals session.StructuredLearningEngramSessionId
            where session.PersonId == personId && step.StructuredLearningDocumentSectionId != null
            select step
        ).ToListAsync(cancellationToken);

        var stepEndBounds = await GetStepEndBoundsAsync(stepRows, cancellationToken);

        var recallRows = await _dbContext.StructuredLearningRecallAttempts
            .AsNoTracking()
            .Where(a => a.PersonId == personId)
            .Select(a => new { a.StructuredLearningDocumentSectionId, a.RecallScore })
            .ToListAsync(cancellationToken);

        var roundRows = await _dbContext.StructuredLearningAssessmentRounds
            .AsNoTracking()
            .Include(r => r.Questions)
            .Where(r => r.PersonId == personId)
            .ToListAsync(cancellationToken);

        var usageRows = await _dbContext.StructuredLearningEngramRuntimeUsages
            .AsNoTracking()
            .Where(u => u.PersonId == personId && u.StructuredLearningDocumentSectionId != null)
            .ToListAsync(cancellationToken);

        var sectionIds = stepRows.Select(s => s.StructuredLearningDocumentSectionId!.Value)
            .Concat(recallRows.Select(a => a.StructuredLearningDocumentSectionId))
            .Concat(roundRows.Select(r => r.StructuredLearningDocumentSectionId))
            .Concat(usageRows.Select(u => u.StructuredLearningDocumentSectionId!.Value))
            .Distinct()
            .ToList();

        if (sectionIds.Count == 0) return [];

        // Azure Speech (TextToSpeech) audio is generated once per engram and
        // then reused by whichever student plays it back — it is a shared
        // content cost, not this one person's own spend. Unlike every other
        // metric here (Recall/Assessment/Voice usage — real per-student
        // interaction), TTS usage is rolled up for the WHOLE engram across
        // every student who triggered it, not filtered to this personId
        // (2026-10-03 — otherwise a student who never personally generated
        // narration audio always saw $0.0000 even when the engram itself
        // has real Azure Speech spend from other students).
        var ttsUsageRows = await _dbContext.StructuredLearningEngramRuntimeUsages
            .AsNoTracking()
            .Where(u => u.Channel == "TextToSpeech" && u.StructuredLearningDocumentSectionId != null && sectionIds.Contains(u.StructuredLearningDocumentSectionId!.Value))
            .ToListAsync(cancellationToken);

        var sections = await _dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Where(s => sectionIds.Contains(s.StructuredLearningDocumentSectionId))
            .Select(s => new { s.StructuredLearningDocumentSectionId, s.StructuredLearningDocumentId, s.ChapterTitle, s.SubchapterTitle, s.SortOrder })
            .ToListAsync(cancellationToken);

        var documentIds = sections.Select(s => s.StructuredLearningDocumentId).Distinct().ToList();
        var documentNames = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .Where(d => documentIds.Contains(d.StructuredLearningDocumentId))
            .Select(d => new { d.StructuredLearningDocumentId, d.DocumentName })
            .ToDictionaryAsync(d => d.StructuredLearningDocumentId, d => d.DocumentName, cancellationToken);

        var result = new List<PersonEngramStatRow>();
        foreach (var section in sections)
        {
            var sectionId = section.StructuredLearningDocumentSectionId;
            var stepsForSection = stepRows.Where(s => s.StructuredLearningDocumentSectionId == sectionId).ToList();
            var stepStats = BuildStepStats(stepsForSection, stepEndBounds);
            var recallScores = recallRows.Where(a => a.StructuredLearningDocumentSectionId == sectionId).Select(a => a.RecallScore).ToList();
            var rounds = roundRows.Where(r => r.StructuredLearningDocumentSectionId == sectionId).ToList();
            var usage = usageRows.Where(u => u.StructuredLearningDocumentSectionId == sectionId).ToList();
            var ttsUsageForSection = ttsUsageRows.Where(u => u.StructuredLearningDocumentSectionId == sectionId).ToList();

            result.Add(new PersonEngramStatRow
            {
                StructuredLearningDocumentId = section.StructuredLearningDocumentId,
                DocumentName = documentNames.GetValueOrDefault(section.StructuredLearningDocumentId, string.Empty),
                StructuredLearningDocumentSectionId = sectionId,
                ChapterTitle = section.ChapterTitle,
                SubchapterTitle = section.SubchapterTitle,
                SortOrder = section.SortOrder,
                TotalSessionsVisited = stepsForSection.Select(s => s.StructuredLearningEngramSessionId).Distinct().Count(),
                TotalTimeSeconds = (int)(stepStats.Sum(s => s.DurationMilliseconds) / 1000),
                VoiceSessionCount = usage.Count(u => u.Channel == "Voice" && u.DurationSeconds > 0),
                Recall = BuildRecallStat(recallScores),
                Assessment = BuildAssessmentStat(rounds),
                Usage = BuildUsageStats(usage),
                UsageCostBreakdown = BuildUsageCostBreakdown(usage),
                TotalInputTokens = usage.Sum(u => u.InputTokens),
                TotalOutputTokens = usage.Sum(u => u.OutputTokens),
                TextToSpeechCharacterCount = ttsUsageForSection.Sum(u => (long)u.CharacterCount),
                TextToSpeechEstimatedCostUsd = EstimateTextToSpeechCost(ttsUsageForSection),
            });
        }

        return result.OrderBy(r => r.DocumentName).ThenBy(r => r.SortOrder).ToList();
    }

    /// <summary>One engram's latest attempt for one student — course name,
    /// engram title, order, score, status, and completion date. The single
    /// shared shape both <see cref="TecContigoContextBuilder"/> and
    /// <see cref="BuddyContextBuilder"/> format into their own
    /// bilingual text, instead of each re-querying/duplicating this same
    /// "latest round per section" reduction (2026-10-03).</summary>
    public sealed class PersonEngramCompletionRow
    {
        public Guid StructuredLearningDocumentSectionId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string EngramTitle { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int? FinalScore { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CompletedDate { get; set; }
    }

    /// <summary>Every Structured Learning engram this person has attempted,
    /// reduced to each section's single latest round — the shared source of
    /// truth for "how many/which engrams has this person completed",
    /// consumed by both tecBot's and Buddy's context builders so neither
    /// hand-rolls its own copy of this query.</summary>
    public async Task<List<PersonEngramCompletionRow>> GetPersonEngramCompletionsAsync(Guid personId, CancellationToken cancellationToken)
    {
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

        return allRounds
            .GroupBy(round => round.StructuredLearningDocumentSectionId)
            .Select(group => group.OrderByDescending(round => round.CreatedDate).First())
            .Select(round => new PersonEngramCompletionRow
            {
                StructuredLearningDocumentSectionId = round.StructuredLearningDocumentSectionId,
                CourseName = round.CourseName,
                EngramTitle = round.EngramTitle,
                SortOrder = round.SortOrder,
                FinalScore = round.FinalScore,
                Status = round.Status,
                CompletedDate = round.CompletedDate ?? round.CreatedDate,
            })
            .ToList();
    }

    /// <summary>Full stats for ONE course-visit session — duration, per-step
    /// timing, Recall pass/fail counts, Assessment pass/fail counts, tokens.</summary>
    public async Task<SessionStatsResult?> GetSessionStatsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _dbContext.StructuredLearningEngramSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StructuredLearningEngramSessionId == sessionId, cancellationToken);
        if (session is null) return null;

        var stepRows = await _dbContext.StructuredLearningEngramSessionSteps
            .AsNoTracking()
            .Where(s => s.StructuredLearningEngramSessionId == sessionId)
            .ToListAsync(cancellationToken);

        var recallAttempts = await _dbContext.StructuredLearningRecallAttempts
            .AsNoTracking()
            .Where(a => a.StructuredLearningEngramSessionId == sessionId)
            .Select(a => a.RecallScore)
            .ToListAsync(cancellationToken);

        var rounds = await _dbContext.StructuredLearningAssessmentRounds
            .AsNoTracking()
            .Include(r => r.Questions)
            .Where(r => r.StructuredLearningEngramSessionId == sessionId)
            .ToListAsync(cancellationToken);

        var usageRows = await _dbContext.StructuredLearningEngramRuntimeUsages
            .AsNoTracking()
            .Where(u => u.StructuredLearningEngramSessionId == sessionId)
            .ToListAsync(cancellationToken);

        var steps = BuildStepStats(stepRows, sessionId, session.CompletedDate ?? session.LastActivityDate);
        return new SessionStatsResult
        {
            StructuredLearningEngramSessionId = session.StructuredLearningEngramSessionId,
            StartedDate = session.StartedDate,
            CompletedDate = session.CompletedDate,
            DurationSeconds = (int)(steps.Sum(s => s.DurationMilliseconds) / 1000),
            Steps = steps,
            Recall = BuildRecallStat(recallAttempts),
            Assessment = BuildAssessmentStat(rounds),
            Usage = BuildUsageStats(usageRows),
        };
    }

    /// <summary>Aggregated stats for ONE engram (section) across every
    /// session and student that ever visited it.</summary>
    /// <summary>Aggregated stats for ONE engram (section) — across every
    /// session/student that ever visited it when <paramref name="personId"/>
    /// is null, or scoped to just that one person's own sessions/attempts/
    /// rounds/usage when provided (2026-10-03, powers the admin "per user"
    /// course dashboard).</summary>
    public async Task<EngramStatsResult> GetEngramStatsAsync(Guid sectionId, Guid? personId, CancellationToken cancellationToken)
    {
        var stepRowsQuery = _dbContext.StructuredLearningEngramSessionSteps
            .AsNoTracking()
            .Where(s => s.StructuredLearningDocumentSectionId == sectionId);
        var stepRows = personId is null
            ? await stepRowsQuery.ToListAsync(cancellationToken)
            : await (
                from step in stepRowsQuery
                join session in _dbContext.StructuredLearningEngramSessions.AsNoTracking()
                    on step.StructuredLearningEngramSessionId equals session.StructuredLearningEngramSessionId
                where session.PersonId == personId.Value
                select step
            ).ToListAsync(cancellationToken);

        var recallAttemptsQuery = _dbContext.StructuredLearningRecallAttempts
            .AsNoTracking()
            .Where(a => a.StructuredLearningDocumentSectionId == sectionId);
        if (personId is not null) recallAttemptsQuery = recallAttemptsQuery.Where(a => a.PersonId == personId.Value);
        var recallAttempts = await recallAttemptsQuery.Select(a => a.RecallScore).ToListAsync(cancellationToken);

        var roundsQuery = _dbContext.StructuredLearningAssessmentRounds
            .AsNoTracking()
            .Include(r => r.Questions)
            .Where(r => r.StructuredLearningDocumentSectionId == sectionId);
        if (personId is not null) roundsQuery = roundsQuery.Where(r => r.PersonId == personId.Value);
        var rounds = await roundsQuery.ToListAsync(cancellationToken);

        var usageRowsQuery = _dbContext.StructuredLearningEngramRuntimeUsages
            .AsNoTracking()
            .Where(u => u.StructuredLearningDocumentSectionId == sectionId);
        if (personId is not null) usageRowsQuery = usageRowsQuery.Where(u => u.PersonId == personId.Value);
        var usageRows = await usageRowsQuery.ToListAsync(cancellationToken);

        var stepEndBounds = await GetStepEndBoundsAsync(stepRows, cancellationToken);
        var stepStats = BuildStepStats(stepRows, stepEndBounds);

        var totalTimeSeconds = (int)(stepStats.Sum(s => s.DurationMilliseconds) / 1000);
        var sessionCount = stepRows.Select(s => s.StructuredLearningEngramSessionId).Distinct().Count();

        return new EngramStatsResult
        {
            StructuredLearningDocumentSectionId = sectionId,
            TotalSessionsVisited = sessionCount,
            TotalTimeSeconds = totalTimeSeconds,
            VoiceSessionCount = usageRows.Count(u => u.Channel == "Voice" && u.DurationSeconds > 0),
            AverageTimeSecondsPerVisit = sessionCount == 0 ? 0 : totalTimeSeconds / sessionCount,
            TotalInputTokens = usageRows.Sum(u => u.InputTokens),
            TotalOutputTokens = usageRows.Sum(u => u.OutputTokens),
            EstimatedCostUsd = EstimateUsageCost(usageRows),
            TextToSpeechCharacterCount = usageRows.Where(u => u.Channel == "TextToSpeech").Sum(u => (long)u.CharacterCount),
            TextToSpeechEstimatedCostUsd = EstimateTextToSpeechCost(usageRows),
            UnassignedInputTokens = usageRows.Where(u => !CanonicalStepTypes.Contains(u.StepType ?? string.Empty)).Sum(u => u.InputTokens),
            UnassignedOutputTokens = usageRows.Where(u => !CanonicalStepTypes.Contains(u.StepType ?? string.Empty)).Sum(u => u.OutputTokens),
            StepBreakdown = stepStats,
            StepSummaries = BuildStepSummaries(stepStats, usageRows),
            Recall = BuildRecallStat(recallAttempts),
            Assessment = BuildAssessmentStat(rounds),
            Usage = BuildUsageStats(usageRows),
            UsageCostBreakdown = BuildUsageCostBreakdown(usageRows),
        };
    }

    /// <summary>Back-compat overload — stats across every session/student.</summary>
    public Task<EngramStatsResult> GetEngramStatsAsync(Guid sectionId, CancellationToken cancellationToken) =>
        GetEngramStatsAsync(sectionId, personId: null, cancellationToken);

    /// <summary>Full cross-course learning dashboard for ONE person
    /// (2026-10-03) — discovers every course the person is enrolled in OR
    /// has any real activity on (sessions/Recall/Assessment/usage), then
    /// bulk-loads (not per-section looped) every stats row scoped to that
    /// person across all those courses' sections, and rolls it up both
    /// overall and per-course. Powers the admin "Users" person detail
    /// page's "Dashboard" tab (KPIs + bar/pie charts).</summary>
    public async Task<PersonLearningDashboardResult> GetPersonLearningDashboardAsync(Guid personId, CancellationToken cancellationToken)
    {
        var enrolledDocIds = await _dbContext.StructuredLearningDocumentEnrollments
            .AsNoTracking()
            .Where(e => e.PersonId == personId)
            .Select(e => e.StructuredLearningDocumentId)
            .ToListAsync(cancellationToken);

        var sessionSectionIds = await (
            from step in _dbContext.StructuredLearningEngramSessionSteps.AsNoTracking()
            join session in _dbContext.StructuredLearningEngramSessions.AsNoTracking()
                on step.StructuredLearningEngramSessionId equals session.StructuredLearningEngramSessionId
            where session.PersonId == personId && step.StructuredLearningDocumentSectionId != null
            select step.StructuredLearningDocumentSectionId!.Value
        ).Distinct().ToListAsync(cancellationToken);

        var recallSectionIds = await _dbContext.StructuredLearningRecallAttempts
            .AsNoTracking()
            .Where(a => a.PersonId == personId)
            .Select(a => a.StructuredLearningDocumentSectionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var roundSectionIds = await _dbContext.StructuredLearningAssessmentRounds
            .AsNoTracking()
            .Where(r => r.PersonId == personId)
            .Select(r => r.StructuredLearningDocumentSectionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var usageSectionIds = await _dbContext.StructuredLearningEngramRuntimeUsages
            .AsNoTracking()
            .Where(u => u.PersonId == personId && u.StructuredLearningDocumentSectionId != null)
            .Select(u => u.StructuredLearningDocumentSectionId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var touchedSectionIds = sessionSectionIds
            .Concat(recallSectionIds)
            .Concat(roundSectionIds)
            .Concat(usageSectionIds)
            .Distinct()
            .ToList();

        var docIdsFromActivity = touchedSectionIds.Count == 0
            ? []
            : await _dbContext.StructuredLearningDocumentSections
                .AsNoTracking()
                .Where(s => touchedSectionIds.Contains(s.StructuredLearningDocumentSectionId))
                .Select(s => s.StructuredLearningDocumentId)
                .Distinct()
                .ToListAsync(cancellationToken);

        var allDocIds = docIdsFromActivity.Concat(enrolledDocIds).Distinct().ToList();
        if (allDocIds.Count == 0) return new PersonLearningDashboardResult();

        var documents = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .Include(d => d.Sections)
            .Where(d => allDocIds.Contains(d.StructuredLearningDocumentId))
            .ToListAsync(cancellationToken);

        var allSectionIds = documents.SelectMany(d => d.Sections.Select(s => s.StructuredLearningDocumentSectionId)).ToList();

        var stepRows = allSectionIds.Count == 0
            ? []
            : await (
                from step in _dbContext.StructuredLearningEngramSessionSteps.AsNoTracking()
                join session in _dbContext.StructuredLearningEngramSessions.AsNoTracking()
                    on step.StructuredLearningEngramSessionId equals session.StructuredLearningEngramSessionId
                where session.PersonId == personId
                    && step.StructuredLearningDocumentSectionId != null
                    && allSectionIds.Contains(step.StructuredLearningDocumentSectionId!.Value)
                select step
            ).ToListAsync(cancellationToken);

        var recallRows = allSectionIds.Count == 0
            ? []
            : await _dbContext.StructuredLearningRecallAttempts
                .AsNoTracking()
                .Where(a => a.PersonId == personId && allSectionIds.Contains(a.StructuredLearningDocumentSectionId))
                .ToListAsync(cancellationToken);

        var roundRows = allSectionIds.Count == 0
            ? []
            : await _dbContext.StructuredLearningAssessmentRounds
                .AsNoTracking()
                .Include(r => r.Questions)
                .Where(r => r.PersonId == personId && allSectionIds.Contains(r.StructuredLearningDocumentSectionId))
                .ToListAsync(cancellationToken);

        var usageRows = allSectionIds.Count == 0
            ? []
            : await _dbContext.StructuredLearningEngramRuntimeUsages
                .AsNoTracking()
                .Where(u => u.PersonId == personId && u.StructuredLearningDocumentSectionId != null && allSectionIds.Contains(u.StructuredLearningDocumentSectionId!.Value))
                .ToListAsync(cancellationToken);

        var stepEndBounds = await GetStepEndBoundsAsync(stepRows, cancellationToken);
        var stepStats = BuildStepStats(stepRows, stepEndBounds);

        var courses = new List<PersonCourseDashboardRow>();
        foreach (var document in documents)
        {
            var sectionIdsForDoc = document.Sections.Select(s => s.StructuredLearningDocumentSectionId).ToHashSet();
            var docSteps = stepStats.Where(s => s.StructuredLearningDocumentSectionId.HasValue && sectionIdsForDoc.Contains(s.StructuredLearningDocumentSectionId.Value)).ToList();
            var docUsage = usageRows.Where(u => u.StructuredLearningDocumentSectionId.HasValue && sectionIdsForDoc.Contains(u.StructuredLearningDocumentSectionId.Value)).ToList();
            var docRounds = roundRows.Where(r => sectionIdsForDoc.Contains(r.StructuredLearningDocumentSectionId)).ToList();
            var completedSectionCount = docRounds
                .Where(r => r.Status == StructuredLearningAssessmentRoundStatus.Passed)
                .Select(r => r.StructuredLearningDocumentSectionId)
                .Distinct()
                .Count();
            var sectionCount = document.Sections.Count;

            courses.Add(new PersonCourseDashboardRow
            {
                StructuredLearningDocumentId = document.StructuredLearningDocumentId,
                DocumentName = document.DocumentName,
                SectionCount = sectionCount,
                CompletedSectionCount = completedSectionCount,
                IsCompleted = sectionCount > 0 && completedSectionCount >= sectionCount,
                TotalTimeSeconds = (int)(docSteps.Sum(s => s.DurationMilliseconds) / 1000),
                TotalInputTokens = docUsage.Sum(u => u.InputTokens),
                TotalOutputTokens = docUsage.Sum(u => u.OutputTokens),
                EstimatedCostUsd = EstimateUsageCost(docUsage),
            });
        }

        return new PersonLearningDashboardResult
        {
            CoursesCompleted = courses.Count(c => c.IsCompleted),
            CoursesInProgress = courses.Count(c => !c.IsCompleted),
            TotalEngramsCompleted = courses.Sum(c => c.CompletedSectionCount),
            TotalEngramsAvailable = courses.Sum(c => c.SectionCount),
            TotalInputTokens = usageRows.Sum(u => u.InputTokens),
            TotalOutputTokens = usageRows.Sum(u => u.OutputTokens),
            CourseEstimatedCostUsd = EstimateUsageCost(usageRows),
            TotalEstimatedCostUsd = EstimateUsageCost(usageRows), // Buddy cost merged in by GetPersonLearningDashboardFunction.
            TotalTimeSeconds = (int)(stepStats.Sum(s => s.DurationMilliseconds) / 1000), // Buddy time merged in by the Function too.
            Recall = BuildRecallStat(recallRows.Select(r => r.RecallScore).ToList()),
            Assessment = BuildAssessmentStat(roundRows),
            Courses = courses.OrderByDescending(c => c.TotalInputTokens + c.TotalOutputTokens).ToList(),
        };
    }

    /// <summary>Loads the whole course timeline so a step ends no later than
    /// the next step, even in another engram. Repairs reporting for historical
    /// missing or late completion events without rewriting stored timestamps.</summary>
    private async Task<Dictionary<Guid, DateTime>> GetStepEndBoundsAsync(List<StructuredLearningEngramSessionStep> rows, CancellationToken cancellationToken)
    {
        var sessionIds = rows.Select(s => s.StructuredLearningEngramSessionId).Distinct().ToList();
        if (sessionIds.Count == 0) return [];
        var sessionEndBounds = await _dbContext.StructuredLearningEngramSessions
            .AsNoTracking()
            .Where(s => sessionIds.Contains(s.StructuredLearningEngramSessionId))
            .ToDictionaryAsync(s => s.StructuredLearningEngramSessionId, s => s.CompletedDate ?? s.LastActivityDate, cancellationToken);
        var timeline = await _dbContext.StructuredLearningEngramSessionSteps
            .AsNoTracking()
            .Where(s => sessionIds.Contains(s.StructuredLearningEngramSessionId))
            .ToListAsync(cancellationToken);
        return BuildStepEndBounds(timeline, sessionEndBounds);
    }

    private static Dictionary<Guid, DateTime> BuildStepEndBounds(
        List<StructuredLearningEngramSessionStep> rows, Dictionary<Guid, DateTime> sessionEndBounds)
    {
        var bounds = new Dictionary<Guid, DateTime>();
        foreach (var sessionSteps in rows.GroupBy(s => s.StructuredLearningEngramSessionId))
        {
            var starts = sessionSteps.GroupBy(s => s.StartedDate).OrderBy(g => g.Key).ToList();
            var sessionEnd = sessionEndBounds.GetValueOrDefault(sessionSteps.Key, DateTime.UtcNow);
            for (var i = 0; i < starts.Count; i++)
            {
                var end = i + 1 < starts.Count && starts[i + 1].Key < sessionEnd
                    ? starts[i + 1].Key : sessionEnd;
                foreach (var step in starts[i])
                {
                    bounds[step.StructuredLearningEngramSessionStepId] = end;
                }
            }
        }
        return bounds;
    }

    private static List<StepStat> BuildStepStats(List<StructuredLearningEngramSessionStep> rows, Dictionary<Guid, DateTime> stepEndBounds)
    {
        var now = DateTime.UtcNow;
        return rows.Select(s =>
        {
            var endBound = stepEndBounds.GetValueOrDefault(s.StructuredLearningEngramSessionStepId, now);
            if (s.CompletedDate is DateTime completed && completed < endBound) endBound = completed;
            if (endBound > now) endBound = now;
            var durationMilliseconds = s.ActiveDurationMilliseconds
                ?? Math.Max(0, (long)(endBound - s.StartedDate).TotalMilliseconds);
            return new StepStat
            {
                SessionId = s.StructuredLearningEngramSessionId,
                StructuredLearningDocumentSectionId = s.StructuredLearningDocumentSectionId,
                StepType = s.StepType,
                StartedDate = s.StartedDate,
                CompletedDate = s.CompletedDate,
                DurationSeconds = (int)(durationMilliseconds / 1000),
                DurationMilliseconds = durationMilliseconds,
                IsEstimated = s.ActiveDurationMilliseconds == null,
            };
        }).ToList();
    }

    /// <summary>Overload for the single-session path, which already knows
    /// its own end boundary without a lookup.</summary>
    private static List<StepStat> BuildStepStats(List<StructuredLearningEngramSessionStep> rows, Guid sessionId, DateTime endBound) =>
        BuildStepStats(rows, BuildStepEndBounds(rows, new Dictionary<Guid, DateTime> { [sessionId] = endBound }));

    internal static int CalculateSessionDuration(List<StructuredLearningEngramSessionStep> rows, Guid sessionId, DateTime endBound) =>
        (int)(BuildStepStats(rows, sessionId, endBound).Sum(s => s.DurationMilliseconds) / 1000);

    /// <summary>One row per canonical pedagogy step (fixed order, always all
    /// 6 even with zero data) — merges step timing with token usage now
    /// tagged by StepType, split by Voice/Text channel, plus model name(s)
    /// and estimated cost via <see cref="TokenCostEstimator"/>.</summary>
    private static readonly string[] CanonicalStepTypes = ["Hypothesis", "Teaching", "DeepDive", "Production", "Recall", "Assessment"];

    private List<StepSummary> BuildStepSummaries(List<StepStat> stepStats, List<StructuredLearningEngramRuntimeUsage> usageRows)
    {
        return CanonicalStepTypes.Select(stepType =>
        {
            var matchingSteps = stepStats.Where(s => s.StepType == stepType).ToList();
            var matchingUsage = usageRows.Where(u => u.StepType == stepType).ToList();
            return new StepSummary
            {
                StepType = stepType,
                SessionsCount = matchingSteps.Select(s => s.SessionId).Distinct().Count(),
                TotalDurationSeconds = (int)(matchingSteps.Sum(s => s.DurationMilliseconds) / 1000),
                AverageDurationSeconds = matchingSteps.Count == 0 ? 0
                    : (int)(matchingSteps.Sum(s => s.DurationMilliseconds) / 1000
                        / matchingSteps.Select(s => s.SessionId).Distinct().Count()),
                HasEstimatedTiming = matchingSteps.Any(s => s.IsEstimated),
                InputTokens = matchingUsage.Sum(u => u.InputTokens),
                OutputTokens = matchingUsage.Sum(u => u.OutputTokens),
                VoiceInputTokens = matchingUsage.Where(u => u.Channel == "Voice").Sum(u => u.InputTokens),
                VoiceOutputTokens = matchingUsage.Where(u => u.Channel == "Voice").Sum(u => u.OutputTokens),
                TextInputTokens = matchingUsage.Where(u => u.Channel == "Text").Sum(u => u.InputTokens),
                TextOutputTokens = matchingUsage.Where(u => u.Channel == "Text").Sum(u => u.OutputTokens),
                ModelNames = string.Join(", ", matchingUsage.Select(u => u.ModelName).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct()),
                EstimatedCostUsd = EstimateUsageCost(matchingUsage),
            };
        }).ToList();
    }

    private List<UsageCostStat> BuildUsageCostBreakdown(List<StructuredLearningEngramRuntimeUsage> usageRows)
    {
        return usageRows
            .GroupBy(u => new
            {
                StepType = string.IsNullOrWhiteSpace(u.StepType) ? "(unassigned)" : u.StepType!,
                u.Channel,
                ModelName = string.IsNullOrWhiteSpace(u.ModelName) ? "(unknown model)" : u.ModelName!,
            })
            .Select(group =>
            {
                var modelUsage = group.Select(u => new AgentTokenUsage
                {
                    AgentName = "StructuredLearningQaProfessorAgent",
                    ModelName = group.Key.ModelName == "(unknown model)" ? string.Empty : group.Key.ModelName,
                    InputTokens = u.InputTokens,
                    OutputTokens = u.OutputTokens,
                }).ToList();
                var pricing = TokenCostEstimator.EstimateByModel(modelUsage, _configuration).Single();
                var inputTokens = group.Sum(u => u.InputTokens);
                var outputTokens = group.Sum(u => u.OutputTokens);
                var inputCostUsd = inputTokens / 1_000_000m * pricing.InputRatePerMillionTokens;
                var outputCostUsd = outputTokens / 1_000_000m * pricing.OutputRatePerMillionTokens;

                return new UsageCostStat
                {
                    StepType = group.Key.StepType,
                    Channel = group.Key.Channel,
                    ModelName = group.Key.ModelName,
                    CallCount = group.Count(),
                    InputTokens = inputTokens,
                    InputRatePerMillionTokens = pricing.InputRatePerMillionTokens,
                    InputCostUsd = inputCostUsd,
                    OutputTokens = outputTokens,
                    OutputRatePerMillionTokens = pricing.OutputRatePerMillionTokens,
                    OutputCostUsd = outputCostUsd,
                    TotalCostUsd = inputCostUsd + outputCostUsd,
                };
            })
            .OrderByDescending(row => row.TotalCostUsd)
            .ToList();
    }

    /// <summary>Real USD estimate for a set of usage rows, via the same
    /// per-model rate table every other cost dashboard in this app uses.</summary>
    private decimal EstimateUsageCost(List<StructuredLearningEngramRuntimeUsage> usageRows)
    {
        var tokenUsage = usageRows
            .Select(u => new AgentTokenUsage
            {
                AgentName = "StructuredLearningQaProfessorAgent",
                ModelName = u.ModelName ?? string.Empty,
                InputTokens = u.InputTokens,
                OutputTokens = u.OutputTokens,
            })
            .ToList();
        return TokenCostEstimator.Estimate(tokenUsage, illustrationsGeneratedCount: 0, _configuration).TotalCostUsd
            + EstimateTextToSpeechCost(usageRows);
    }

    private decimal EstimateTextToSpeechCost(List<StructuredLearningEngramRuntimeUsage> usageRows)
    {
        var raw = _configuration["AgentPricing:AzureSpeechNeuralPerMillionCharactersUsd"];
        var rate = decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var configured)
            ? configured
            : 16m;
        return Math.Round(usageRows.Where(u => u.Channel == "TextToSpeech")
            .Sum(u => (decimal)u.CharacterCount) / 1_000_000m * rate, 4);
    }


    private static RecallStat BuildRecallStat(List<int> scores) => new()
    {
        AttemptsCount = scores.Count,
        MasteredCount = scores.Count(s => RecallLoopGate.IsMastered(s)),
        FailedCount = scores.Count(s => !RecallLoopGate.IsMastered(s)),
        AverageScore = scores.Count == 0 ? null : scores.Average()
    };

    private static AssessmentStat BuildAssessmentStat(List<StructuredLearningAssessmentRound> rounds) => new()
    {
        RoundsCount = rounds.Count,
        PassedRounds = rounds.Count(r => r.Status == StructuredLearningAssessmentRoundStatus.Passed),
        FailedRounds = rounds.Count(r => r.Status == StructuredLearningAssessmentRoundStatus.Failed),
        QuestionsCount = rounds.Sum(r => r.Questions.Count),
        IncorrectAnswers = rounds.Sum(r => r.Questions.Count(q => q.Correctness == StructuredLearningAssessmentAnswerCorrectness.Incorrect))
    };

    private static List<UsageStat> BuildUsageStats(List<StructuredLearningEngramRuntimeUsage> rows) =>
        rows.GroupBy(u => new { u.Channel, u.ModelName })
            .Select(g => new UsageStat
            {
                Channel = g.Key.Channel,
                ModelName = g.Key.ModelName,
                InputTokens = g.Sum(u => u.InputTokens),
                OutputTokens = g.Sum(u => u.OutputTokens),
                DurationSeconds = g.Sum(u => u.DurationSeconds)
            }).ToList();

    /// <summary>Real "Reactivar Engrama" (Brain spaced-repetition review)
    /// spend for ONE person (2026-10-04) — reads every row ever recorded
    /// in <see cref="Models.Learning.EngramReviewSessionUsage"/> for them,
    /// groups by node, and converts duration into $ using the same
    /// Realtime-per-minute rate as every other voice feature. Real
    /// measured data only — no proxy, since this table is only populated
    /// by actual session teardowns.</summary>
    public async Task<PersonEngramReviewUsageResult> GetPersonEngramReviewUsageAsync(Guid personId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.EngramReviewSessionUsages
            .AsNoTracking()
            .Where(u => u.PersonId == personId)
            .ToListAsync(cancellationToken);

        var result = new PersonEngramReviewUsageResult
        {
            TotalSessions = rows.Count,
            TotalDurationSeconds = rows.Sum(r => r.DurationSeconds),
        };

        var realtimePerMinuteUsd = ReadDecimalConfig("AgentPricing:RealtimePerMinuteUsd", 0.045m);
        result.EstimatedCostUsd = Math.Round(result.TotalDurationSeconds / 60m * realtimePerMinuteUsd, 4);

        var nodeIds = rows.Where(r => r.CapabilityGraphNodeId.HasValue)
            .Select(r => r.CapabilityGraphNodeId!.Value)
            .Distinct()
            .ToList();

        var nodeNames = nodeIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.CapabilityGraphNodes
                .AsNoTracking()
                .Where(n => nodeIds.Contains(n.CapabilityGraphNodeId))
                .Select(n => new { n.CapabilityGraphNodeId, n.Name })
                .ToDictionaryAsync(n => n.CapabilityGraphNodeId, n => n.Name, cancellationToken);

        result.Nodes = rows
            .Where(r => r.CapabilityGraphNodeId.HasValue)
            .GroupBy(r => r.CapabilityGraphNodeId!.Value)
            .Select(g =>
            {
                var durationSeconds = g.Sum(r => r.DurationSeconds);
                return new PersonEngramReviewNodeRow
                {
                    CapabilityGraphNodeId = g.Key,
                    NodeName = nodeNames.TryGetValue(g.Key, out var name) ? name : "—",
                    SessionCount = g.Count(),
                    TotalDurationSeconds = durationSeconds,
                    EstimatedCostUsd = Math.Round(durationSeconds / 60m * realtimePerMinuteUsd, 4),
                };
            })
            .OrderByDescending(n => n.EstimatedCostUsd)
            .ToList();

        return result;
    }

    private decimal ReadDecimalConfig(string key, decimal fallback)
    {
        var raw = _configuration[key];
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
