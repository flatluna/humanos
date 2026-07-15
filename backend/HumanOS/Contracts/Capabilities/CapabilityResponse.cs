namespace HumanOS.Contracts.Capabilities;

using HumanOS.Agents.Studio;

public sealed class CapabilityResponse
{
    public Guid CapabilityId { get; set; }

    public Guid CapabilityDomainId { get; set; }

    public string DomainCode { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public int LevelCount { get; set; }

    public int ModuleCount { get; set; }

    /// <summary>Which Human Evolution Layers this capability's levels
    /// cover (e.g. ["Foundation", "Exploration", "Mastery"]).</summary>
    public List<HumanEvolutionLayer> Levels { get; set; } = [];

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
