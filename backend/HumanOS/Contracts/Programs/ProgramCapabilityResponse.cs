namespace HumanOS.Contracts.Programs;

/// <summary>One Capability's membership within a Program's sequence —
/// enriched with just enough Capability data for the designer's picker
/// and a student-facing program landing page to render without a second
/// round-trip per capability.</summary>
public sealed class ProgramCapabilityResponse
{
    public Guid ProgramCapabilityId { get; set; }

    public Guid CapabilityId { get; set; }

    public string CapabilityCode { get; set; } = null!;

    public string CapabilityName { get; set; } = null!;

    public string? SubjectCode { get; set; }

    public int SortOrder { get; set; }

    public bool IsRequired { get; set; }

    public string? PhaseLabel { get; set; }

    /// <summary>This Capability's objectives/requirements specifically
    /// within this Program's sequence — see ProgramCapability.cs.</summary>
    public string? Objectives { get; set; }

    public string? Requirements { get; set; }

    public int LevelCount { get; set; }

    public int NodeCount { get; set; }

    public bool HasCoverImage { get; set; }
}
