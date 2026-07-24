namespace HumanOS.Contracts.Programs;

/// <summary>Catalog list item for GET /programs — mirrors CapabilityResponse's
/// role for the Capability catalog.</summary>
public class ProgramResponse
{
    public Guid ProgramId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? Objectives { get; set; }

    public string? Requirements { get; set; }

    public bool HasLogo { get; set; }

    public bool IsActive { get; set; }

    public int CapabilityCount { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
