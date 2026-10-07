namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// One real student "entry" into an engram (StructuredLearningDocumentSection)
/// runtime (2026-10-02) — powers "cuántas veces entró", entry day/time, and
/// total time spent across all 6 steps (Hypothesis/Teaching/DeepDive/
/// Production/Recall/Assessment). Voice/Text Professor token usage (see
/// <see cref="StructuredLearningEngramRuntimeUsage"/>) is linked to the
/// session active when that usage happened, so cost can also be rolled up
/// per real visit, not just per person for all time.
/// </summary>
public sealed class StructuredLearningEngramSession
{
    public Guid StructuredLearningEngramSessionId { get; set; } = Guid.NewGuid();

    public Guid? PersonId { get; set; }

    /// <summary>The course this engram belongs to.</summary>
    public Guid? StructuredLearningDocumentId { get; set; }

    /// <summary>The engram (section) the student entered.</summary>
    public Guid? StructuredLearningDocumentSectionId { get; set; }

    /// <summary>When the student entered this engram — this visit's "day/hour".</summary>
    public DateTime StartedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Bumped on every step start/complete and usage report, so a
    /// session that never gets an explicit CompletedDate (student left
    /// mid-way) still has a last-known timestamp to report.</summary>
    public DateTime LastActivityDate { get; set; } = DateTime.UtcNow;

    /// <summary>Set once all 6 steps are completed.</summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>CompletedDate - StartedDate, in seconds. Null until completed.</summary>
    public int? DurationSeconds { get; set; }

    /// <summary>Where this visit was launched from (2026-10-06) — null/"Course"
    /// for the normal in-course page, "BrainReview" when the student opened
    /// this engram from the "Mi Cerebro"/Brain mastered-engrams dashboard
    /// instead of the course itself. Lets cost/usage be split by entry point
    /// (see PlatformUsageDashboardService's Brain panel) without creating a
    /// second, duplicate usage ledger — this is the SAME runtime session/
    /// usage mechanism as taking the course, just tagged with where it
    /// started.</summary>
    public string? Source { get; set; }

    public ICollection<StructuredLearningEngramSessionStep> Steps { get; set; } = new List<StructuredLearningEngramSessionStep>();
}
