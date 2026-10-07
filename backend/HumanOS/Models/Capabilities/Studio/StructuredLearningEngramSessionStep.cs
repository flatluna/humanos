namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// One Hypothesis/Teaching/DeepDive/Production/Recall/Assessment step's
/// start/completion timestamps within one <see cref="StructuredLearningEngramSession"/>
/// One row per visible interval; returning to a step creates another interval.
/// Legacy rows have no explicit active duration and use bounded timestamps.
/// </summary>
public sealed class StructuredLearningEngramSessionStep
{
    public Guid StructuredLearningEngramSessionStepId { get; set; } = Guid.NewGuid();

    public Guid StructuredLearningEngramSessionId { get; set; }

    /// <summary>The engram (section) this step happened in (2026-10-02) —
    /// one course session spans every engram the student visits, so this
    /// allows time to be grouped per engram. Ending a visible interval does
    /// not imply that its activity or assessment was passed.</summary>
    public Guid? StructuredLearningDocumentSectionId { get; set; }

    /// <summary>"Hypothesis"/"Teaching"/"DeepDive"/"Production"/"Recall"/"Assessment".</summary>
    public string StepType { get; set; } = string.Empty;

    public DateTime StartedDate { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedDate { get; set; }

    public long? ActiveDurationMilliseconds { get; set; }

    public StructuredLearningEngramSession Session { get; set; } = null!;
}
