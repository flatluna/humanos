namespace HumanOS.Models.Gamification;

/// <summary>
/// Student gamification profile summarizing total points, level progression, and daily streaks.
/// </summary>
public class PlayerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PersonId { get; set; }

    public int TotalPoints { get; set; } = 0;

    public int CurrentLevel { get; set; } = 1;

    public string? CurrentLevelName { get; set; }

    public int CurrentStreakDays { get; set; } = 0;

    public int LongestStreakDays { get; set; } = 0;

    public int StreakFreezesAvailable { get; set; } = 1;

    public DateTime? LastActivityDate { get; set; }

    public int TotalEngramsMastered { get; set; } = 0;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
