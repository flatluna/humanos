namespace HumanOS.Contracts.Dashboard;

/// <summary>One calendar month's totals across EVERY course ever created on
/// the platform (2026-10-06, ALL tenants combined — not scoped to one
/// tenant) — powers the platform-wide "Usage dashboard" → "Creación de
/// cursos" panel. Mirrors <see cref="HumanOS.Contracts.Tenancy.TenantMonthlyCostResponse"/>
/// but global and with richer breakdown (tokens/media counts, not just cost).</summary>
public sealed class PlatformCoursesGeneratedMonthlyResponse
{
    public string YearMonth { get; set; } = string.Empty;
    public int CoursesCreatedCount { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public int ImagesGeneratedCount { get; set; }
    public int VideosGeneratedCount { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}

/// <summary>One calendar month's totals across EVERY course-taking
/// (consumption) interaction on the platform (2026-10-06) — real student
/// sessions, Recall/Assessment/Q&A-with-Professor usage, realtime voice and
/// text-to-speech, ALL tenants combined. Powers the "Toma de cursos" panel.</summary>
public sealed class PlatformCoursesTakenMonthlyResponse
{
    public string YearMonth { get; set; } = string.Empty;
    public int SessionsCount { get; set; }
    public int VoiceSessionCount { get; set; }
    public int VoiceDurationSeconds { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long TextToSpeechCharacterCount { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}

/// <summary>One calendar month's Buddy (Workplace Companion realtime voice)
/// usage across every employee on the platform (2026-10-06).</summary>
public sealed class PlatformBuddyUsageMonthlyResponse
{
    public string YearMonth { get; set; } = string.Empty;
    public int SessionCount { get; set; }
    public int TotalDurationSeconds { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}

/// <summary>One calendar month's usage for ONE model/agent serving
/// in-course Q&amp;A-with-Professor turns (2026-10-06, ALL tenants) — same
/// source rows as <see cref="PlatformCoursesTakenMonthlyResponse"/> but
/// grouped by model/agent instead of totalled, so spend can be attributed
/// to which model answered the questions. Powers "Uso de Agentes (cursos)".</summary>
public sealed class PlatformAgentUsageMonthlyResponse
{
    public string YearMonth { get; set; } = string.Empty;
    public string StepType { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public int CallCount { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}

/// <summary>One calendar month's Labs (simulation) usage across every lab on
/// the platform (2026-10-06) — attempts, realtime-voice attempt duration,
/// and AttemptEvaluationAgent token cost, ALL tenants combined.</summary>
public sealed class PlatformLabsUsageMonthlyResponse
{
    public string YearMonth { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public int VoiceSessionCount { get; set; }
    public int VoiceDurationSeconds { get; set; }
    public int EvaluationCount { get; set; }
    public long EvaluationInputTokens { get; set; }
    public long EvaluationOutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}

/// <summary>One calendar month's "Brain" usage across every employee on the
/// platform (2026-10-06) — TWO distinct sources rolled into one panel:
/// 1) legacy "Reactivar Engrama" spaced-repetition voice reviews
/// (EngramReviewSessionUsage, Capability-graph nodes only), and
/// 2) Structured Learning engrams opened from the Brain mastered-engrams
/// dashboard instead of the course page (tagged via
/// StructuredLearningEngramSession.Source = "BrainReview") — same runtime
/// usage ledger as taking the course, just filtered to ONLY this entry
/// point (deliberately NOT the same rows as the "Uso de Agentes (cursos)"
/// panel, which is a separate cost even though both query the same
/// underlying StructuredLearningEngramRuntimeUsage table — this one is
/// scoped down to Brain-originated sessions only via the Source tag).</summary>
public sealed class PlatformBrainUsageMonthlyResponse
{
    public string YearMonth { get; set; } = string.Empty;
    public int SessionCount { get; set; }
    public int TotalDurationSeconds { get; set; }
    public int BrainStructuredLearningSessionCount { get; set; }
    public long BrainStructuredLearningInputTokens { get; set; }
    public long BrainStructuredLearningOutputTokens { get; set; }
    public int BrainStructuredLearningVoiceDurationSeconds { get; set; }
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>Just the legacy "Reactivar Engrama" (EngramReviewSessionUsage)
    /// slice of <see cref="EstimatedCostUsd"/> (2026-10-06) — lets a
    /// platform-wide grand-total card add this up WITHOUT double-counting
    /// the Structured-Learning-via-Brain slice, which is already included
    /// in <see cref="HumanOS.Contracts.Dashboard.PlatformCoursesTakenMonthlyResponse.EstimatedCostUsd"/>.</summary>
    public decimal LegacyReviewCostUsd { get; set; }

    /// <summary>Brain-only breakdown by learning step/model/channel
    /// (2026-10-06) — ONLY rows whose session was opened from Brain
    /// (Source = "BrainReview"), never the course-page equivalent, so this
    /// never double-counts with Panel 4's "Uso de Agentes (cursos)".</summary>
    public List<PlatformAgentUsageMonthlyResponse> StepBreakdown { get; set; } = [];
}
