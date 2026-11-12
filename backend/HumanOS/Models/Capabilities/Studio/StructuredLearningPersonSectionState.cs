namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// Per-student, per-section runtime state for Structured Learning's
/// Recall/Deep Dive steps (2026-08-13) — replaces reading/writing the
/// shared, single-row <see cref="StructuredLearningSectionStep"/> columns
/// (CurrentRecallPrompt/CurrentDeepDiveCardIndex/CurrentDeepDivePrompt)
/// when a real PersonId is known, since those columns are shared across
/// EVERY student and would let two students overwrite each other's live
/// question. capabilitystudio's author/reviewer preview (no PersonId)
/// keeps using the Step's own shared columns unchanged.
/// </summary>
public sealed class StructuredLearningPersonSectionState
{
    public Guid StructuredLearningPersonSectionStateId { get; set; } = Guid.NewGuid();

    public Guid PersonId { get; set; }

    public Guid StructuredLearningDocumentSectionId { get; set; }

    public string? CurrentRecallPrompt { get; set; }

    /// <summary>Ground-truth concrete scene description backing
    /// <see cref="CurrentRecallPrompt"/> when it's a picture-grounded
    /// question (e.g. "a round pizza cut into 6 equal slices") — carried
    /// forward so the NEXT turn's grading call can verify the answer
    /// against this exact fact instead of needing to actually see the
    /// generated image. Null for non-visual questions.</summary>
    public string? CurrentRecallVisualObjectsDescription { get; set; }

    public int CurrentDeepDiveCardIndex { get; set; }

    public string? CurrentDeepDivePrompt { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public StructuredLearningDocumentSection Section { get; set; } = null!;
}
