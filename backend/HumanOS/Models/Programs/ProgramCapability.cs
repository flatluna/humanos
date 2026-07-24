namespace HumanOS.Models.Programs;

using HumanOS.Models.Capabilities;

/// <summary>
/// Join row placing one existing Capability at a specific position within
/// a Program's recommended learning sequence (the "(1), (2), (3)..." the
/// designer sets in the wizard's capability-picker step).
/// </summary>
public sealed class ProgramCapability
{
    public Guid ProgramCapabilityId { get; set; }

    public Guid ProgramId { get; set; }

    public Guid CapabilityId { get; set; }

    /// <summary>1-based position within the Program's sequence.</summary>
    public int SortOrder { get; set; }

    /// <summary>False marks this Capability as optional/elective within
    /// the Program (still shown, but not required to consider the Program
    /// "complete").</summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>Optional free-text grouping label shown above this
    /// Capability in the sequence (e.g. "Fase 1: Fundamentos"). Null when
    /// the designer didn't group capabilities into phases.</summary>
    public string? PhaseLabel { get; set; }

    /// <summary>Optional: what this specific Capability is meant to
    /// accomplish IN THE CONTEXT of this Program (distinct from the
    /// Program's own overall <see cref="LearningProgram.Objectives"/> and
    /// from the Capability's own general <c>Description</c>). Set by the
    /// designer at Capability-creation time when attaching straight into a
    /// Program's sequence; passed through to GraphArchitectAgent as extra
    /// grounding context when the graph is designed.</summary>
    public string? Objectives { get; set; }

    /// <summary>Optional: prerequisites a learner needs before starting
    /// this Capability WITHIN this Program's sequence (e.g. "haber
    /// completado la capability #3"). Free text, not a hard gate — same
    /// role as <see cref="Objectives"/> above.</summary>
    public string? Requirements { get; set; }

    public DateTime CreatedDate { get; set; }

    public LearningProgram Program { get; set; } = null!;

    public Capability Capability { get; set; } = null!;
}
