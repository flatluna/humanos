namespace HumanOS.Contracts.Projects;

public sealed class ProjectResponse
{
    public Guid ProjectId { get; set; }

    public Guid CapabilityId { get; set; }

    public string CapabilityCode { get; set; } = null!;

    public string CapabilityName { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int DifficultyLevel { get; set; }

    public decimal? EstimatedHours { get; set; }

    public bool IsActive { get; set; }
}
