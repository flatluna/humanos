namespace HumanOS.Contracts.Goals;

public sealed class PersonGoalResponse
{
    public Guid PersonGoalId { get; set; }

    public Guid PersonId { get; set; }

    public Guid GoalId { get; set; }

    public string GoalCode { get; set; } = null!;

    public string GoalName { get; set; } = null!;

    public string? Category { get; set; }

    public string Status { get; set; } = null!;

    public decimal ProgressPercentage { get; set; }

    public DateOnly? TargetDate { get; set; }

    public DateTime StartedDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
