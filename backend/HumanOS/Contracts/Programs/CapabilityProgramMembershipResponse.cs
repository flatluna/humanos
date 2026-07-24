namespace HumanOS.Contracts.Programs;

/// <summary>One Program a Capability belongs to — see
/// ProgramService.GetProgramsForCapabilityAsync, powers the "Programas"
/// section on the Capability's own detail page (a Capability may belong
/// to zero, one, or several Programs).</summary>
public sealed class CapabilityProgramMembershipResponse
{
    public Guid ProgramId { get; set; }

    public string ProgramCode { get; set; } = null!;

    public string ProgramName { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsRequired { get; set; }

    public string? PhaseLabel { get; set; }

    public string? Objectives { get; set; }

    public string? Requirements { get; set; }
}
