namespace HumanOS.Contracts.Projects;

public sealed class PersonProjectResponse
{
    public Guid PersonProjectId { get; set; }

    public Guid PersonId { get; set; }

    public Guid ProjectId { get; set; }

    public string ProjectName { get; set; } = null!;

    public string CapabilityCode { get; set; } = null!;

    public string CapabilityName { get; set; } = null!;

    public int DifficultyLevel { get; set; }

    public decimal? EstimatedHours { get; set; }

    public string Status { get; set; } = null!;

    public decimal ProgressPercentage { get; set; }

    public DateTime? StartedDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
