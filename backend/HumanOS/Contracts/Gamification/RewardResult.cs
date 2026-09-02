namespace HumanOS.Contracts.Gamification;

/// <summary>
/// Result returned after attempting to grant a gamification reward for mastering an Engram.
/// </summary>
public class RewardResult
{
    public bool AlreadyEarned { get; set; }

    public int PointsBase { get; set; }

    public decimal DifficultyMultiplier { get; set; }

    public decimal MemoryEffortMultiplier { get; set; }

    public decimal SurpriseMultiplier { get; set; }

    public int PointsAwarded { get; set; }

    public bool WasSurprise { get; set; }

    public string SurpriseType { get; set; } = "NONE";

    public int NewTotalPoints { get; set; }

    public int NewLevel { get; set; }

    public string NewLevelName { get; set; } = string.Empty;

    public bool LeveledUp { get; set; }
}
