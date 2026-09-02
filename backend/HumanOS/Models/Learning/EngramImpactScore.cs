namespace HumanOS.Models.Learning;

/// <summary>
/// Impact score for an Engram evaluated for an Employee within a Course/Document.
/// </summary>
public class EngramImpactScore
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EmployeeId { get; set; }

    public Guid CourseId { get; set; }

    public string EngramId { get; set; } = null!;

    public int Score { get; set; }

    public string? Tier { get; set; }

    public string? PrimarySignal { get; set; }

    public string? Reason { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
