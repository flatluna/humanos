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

    public int CurrentDeepDiveCardIndex { get; set; }

    public string? CurrentDeepDivePrompt { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public StructuredLearningDocumentSection Section { get; set; } = null!;
}
