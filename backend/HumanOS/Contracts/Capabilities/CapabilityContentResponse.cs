using HumanOS.Agents.Studio;

namespace HumanOS.Contracts.Capabilities;

/// <summary>
/// Full read-only content of a published Capability — levels, modules
/// (with their full instructor Script), and assigned metrics. Used by the
/// "view real generated content" screen (My Courses -> view content),
/// distinct from the lightweight CapabilityResponse (list/summary) which
/// doesn't carry levels/modules at all.
/// </summary>
public sealed class CapabilityContentResponse
{
    public Guid CapabilityId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public List<CapabilityContentLevel> Levels { get; set; } = [];
}

public sealed class CapabilityContentLevel
{
    public Guid CapabilityLevelId { get; set; }

    public HumanEvolutionLayer Layer { get; set; }

    public string Title { get; set; } = null!;

    public string HumanTransformation { get; set; } = null!;

    public List<CapabilityContentModule> Modules { get; set; } = [];
}

public sealed class CapabilityContentModule
{
    public Guid CapabilityModuleId { get; set; }

    public int SortOrder { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public ModuleType Type { get; set; }

    public string Script { get; set; } = null!;

    public string MetricRationale { get; set; } = null!;

    public List<CapabilityMetric> Metrics { get; set; } = [];
}
