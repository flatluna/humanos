namespace HumanOS.Models.Gamification;

/// <summary>
/// Immutable record of each earned reward and points calculation for an engram.
/// </summary>
public class RewardLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PersonId { get; set; }

    public Guid CourseId { get; set; }

    public string EngramId { get; set; } = null!;

    public int PointsBase { get; set; }

    public decimal DifficultyMultiplier { get; set; } = 1.0m;

    public decimal MemoryEffortMultiplier { get; set; } = 1.0m;

    public decimal SurpriseMultiplier { get; set; } = 1.0m;

    public int PointsAwarded { get; set; }

    public bool WasSurprise { get; set; }

    public string? SurpriseType { get; set; }

    public int ImpactScoreAtTime { get; set; }

    public string? Reason { get; set; }

    public string? SourceType { get; set; }

    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
}
