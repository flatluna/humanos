using HumanOS.Agents.Studio;

namespace HumanOS.Models.Capabilities;

public sealed class CapabilityModule
{
    public Guid CapabilityModuleId { get; set; }

    public Guid CapabilityLevelId { get; set; }

    public int SortOrder { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public ModuleType Type { get; set; }

    public string Script { get; set; } = null!;

    public string MetricRationale { get; set; } = null!;

    public DateTime CreatedDate { get; set; }

    public CapabilityLevel CapabilityLevel { get; set; } = null!;

    public ICollection<CapabilityModuleMetric> Metrics { get; set; } = [];

    public ICollection<CapabilityModuleVerification> Verifications { get; set; } = [];

    public ICollection<CapabilityKnowledgeChunk> KnowledgeChunks { get; set; } = [];
}
