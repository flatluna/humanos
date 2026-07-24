namespace HumanOS.Contracts.Goals;

public sealed class GoalResponse
{
    public Guid GoalId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? Category { get; set; }

    public bool IsActive { get; set; }

    public List<CapabilityRefResponse> RequiredCapabilities { get; set; } = new();
}

public sealed class CapabilityRefResponse
{
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;
}
