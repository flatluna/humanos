using HumanOS.Agents.Studio;

namespace HumanOS.Models.Capabilities;

public sealed class CapabilityLevel
{
    public Guid CapabilityLevelId { get; set; }

    public Guid CapabilityId { get; set; }

    public HumanEvolutionLayer Layer { get; set; }

    public int SortOrder { get; set; }

    public string Title { get; set; } = null!;

    public string HumanTransformation { get; set; } = null!;

    public DateTime CreatedDate { get; set; }

    public Capability Capability { get; set; } = null!;

    public ICollection<CapabilityModule> Modules { get; set; } = [];
}
