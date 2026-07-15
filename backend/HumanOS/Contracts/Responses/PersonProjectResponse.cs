namespace HumanOS.Contracts.Responses;

public sealed class PersonProjectResponse
{
    public Guid? PersonProjectId { get; set; }

    public Guid? PersonId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid CapabilityId { get; set; }

    public string CapabilityCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int DifficultyLevel { get; set; }

    public decimal? EstimatedHours { get; set; }

    public bool IsActive { get; set; }

    public string? Status { get; set; }

    public decimal? ProgressPercentage { get; set; }

    public DateTime? StartedDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
