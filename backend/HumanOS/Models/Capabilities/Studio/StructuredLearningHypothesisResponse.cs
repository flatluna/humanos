namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// One student's answer to one Hypothesis pre-check question ("Antes de
/// empezar" / "Before we start") for a Structured Learning section
/// (2026-08-13) — closes the gap where these answers were only ever kept
/// in the browser's localStorage (never sent to the backend, never
/// visible to an instructor, lost on another device). Mirrors
/// <see cref="StructuredLearningRecallAttempt"/>'s PersonId convention:
/// null for capabilitystudio's author/reviewer preview, set for the real
/// student runtime.
/// </summary>
public sealed class StructuredLearningHypothesisResponse
{
    public Guid StructuredLearningHypothesisResponseId { get; set; } = Guid.NewGuid();

    public Guid StructuredLearningDocumentSectionId { get; set; }

    /// <summary>Null for capabilitystudio's author/reviewer preview — set
    /// for the real student runtime.</summary>
    public Guid? PersonId { get; set; }

    /// <summary>0-based index into the Hypothesis step's own question list
    /// (StepDataJson).</summary>
    public int QuestionIndex { get; set; }

    public int SelectedOptionIndex { get; set; }

    /// <summary>Computed in code against the question's own CorrectIndex —
    /// null if the question carries no known correct answer.</summary>
    public bool? IsCorrect { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public StructuredLearningDocumentSection Section { get; set; } = null!;
}
