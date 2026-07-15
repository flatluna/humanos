using HumanOS.Agents.Studio;

namespace HumanOS.Models.Capabilities;

public sealed class CapabilityModuleMetric
{
    public Guid CapabilityModuleId { get; set; }

    public CapabilityMetric Metric { get; set; }

    public CapabilityModule CapabilityModule { get; set; } = null!;
}
