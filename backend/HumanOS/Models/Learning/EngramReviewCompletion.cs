namespace HumanOS.Models.Learning;

/// <summary>
/// One completed "Reactivar Engrama" full voice-review session (product
/// decision 2026-08-07) — recorded ONLY when the Agent itself decides,
/// via the <c>complete_review</c> Realtime tool (see
/// EngramReviewSessionFunction), that it walked the student through the
/// ENTIRE lesson content from beginning to end with genuine engagement.
/// Deliberately separate from <see cref="LearningSessionNode"/> — this
/// feature never creates a graded LearningSession/LearningSessionStep/
/// LearningEvidence (see EngramReviewSessionFunction's doc comment) — yet,
/// per explicit product decision, counts EQUALLY toward the Brain page's
/// forgetting-curve stability/automaticity signals
/// (<see cref="Services.GraphProgressionEngine"/>'s per-node review count
/// and last-completed date) as a graded practice attempt: a full, engaged
/// voice review genuinely refreshes memory the same way practicing again
/// does.
/// </summary>
public class EngramReviewCompletion
{
    public Guid EngramReviewCompletionId { get; set; } = Guid.NewGuid();

    public Guid PersonId { get; set; }

    public Guid CapabilityGraphNodeId { get; set; }

    public DateTime CompletedDate { get; set; } = DateTime.UtcNow;
}
