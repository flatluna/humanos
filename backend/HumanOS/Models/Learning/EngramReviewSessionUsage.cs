namespace HumanOS.Models.Learning;

/// <summary>
/// One real "Reactivar Engrama" voice-review session's duration (2026-10-04)
/// — recorded on EVERY session teardown, regardless of whether the student
/// finished the whole lesson or only reviewed part of it (e.g. a single
/// Deep Dive card) or closed early. Deliberately SEPARATE from
/// <see cref="EngramReviewCompletion"/>, which only records a row when the
/// Agent's own <c>complete_review</c> tool confirms a genuine full-lesson
/// walkthrough — real cost must be captured for actual consumption, not
/// gated behind "did the student finish everything" (a client is never
/// obligated to review the whole thing). Same client-reports-duration
/// pattern as <see cref="People.BuddySessionUsage"/>.
/// </summary>
public sealed class EngramReviewSessionUsage
{
    public Guid EngramReviewSessionUsageId { get; set; } = Guid.NewGuid();

    public Guid? PersonId { get; set; }

    public Guid? CapabilityGraphNodeId { get; set; }

    public int DurationSeconds { get; set; }

    public DateTime CreatedDate { get; set; }
}
