namespace HumanOS.Contracts.Programs;

/// <summary>Body for PUT /programs/{id}/capabilities — the wizard's
/// "select and sequence capabilities" step submits the FULL desired list
/// every time (not incremental diffs); the backend replaces all existing
/// ProgramCapability rows for this Program with this list.</summary>
public sealed class UpdateProgramCapabilitiesRequest
{
    public List<ProgramCapabilityEntry> Capabilities { get; set; } = [];
}

public sealed class ProgramCapabilityEntry
{
    public Guid CapabilityId { get; set; }

    public int SortOrder { get; set; }

    public bool IsRequired { get; set; } = true;

    public string? PhaseLabel { get; set; }
}
