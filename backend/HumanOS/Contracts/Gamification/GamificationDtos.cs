namespace HumanOS.Contracts.Gamification;

/// <summary>
/// Status and level progression details for a player.
/// </summary>
public class PlayerStatusDto
{
    public int TotalPoints { get; set; }

    public int CurrentLevel { get; set; } = 1;

    public string CurrentLevelName { get; set; } = "Neurona";

    public int CurrentStreakDays { get; set; }

    public int LongestStreakDays { get; set; }

    public int StreakFreezesAvailable { get; set; } = 1;

    public int TotalEngramsMastered { get; set; }

    public int PointsToNextLevel { get; set; }

    public string? NextLevelName { get; set; }

    public int TotalAssignedEngrams { get; set; }

    public int TotalAssignedCourses { get; set; }

    public int CompletedCourses { get; set; }

    public int CoursesReady { get; set; }

    public int CoursesPendingGeneration { get; set; }
}

/// <summary>
/// Status of a badge in the catalog for the student (earned or locked).
/// </summary>
public class BadgeStatusDto
{
    public string Code { get; set; } = string.Empty;

    public string? Category { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string IconEmoji { get; set; } = string.Empty;

    public bool IsEarned { get; set; }

    public DateTime? EarnedAt { get; set; }

    public string? RuleType { get; set; }

    public int RuleThreshold { get; set; }
}

/// <summary>
/// Detailed progress towards unlocking a catalog badge.
/// </summary>
public class BadgeProgress
{
    public string Code { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? IconEmoji { get; set; }

    public string? Category { get; set; }

    public string? RuleType { get; set; }

    public int TargetValue { get; set; }

    public int CurrentValue { get; set; }

    public int ProgressPercent { get; set; }

    public bool IsEarned { get; set; }

    public DateTime? EarnedAt { get; set; }
}

/// <summary>
/// Historical entry of a rewarded engram achievement.
/// </summary>
public class RewardHistoryDto
{
    public string EngramId { get; set; } = string.Empty;

    public int PointsAwarded { get; set; }

    public bool WasSurprise { get; set; }

    public string? SurpriseType { get; set; }

    public string? Reason { get; set; }

    public DateTime EarnedAt { get; set; }
}
