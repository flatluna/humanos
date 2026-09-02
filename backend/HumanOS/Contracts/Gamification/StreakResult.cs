namespace HumanOS.Contracts.Gamification;

/// <summary>
/// Result returned after recording daily learning activity for streak progression.
/// </summary>
public class StreakResult
{
    public int CurrentStreakDays { get; set; }

    public int LongestStreakDays { get; set; }

    public int StreakFreezesAvailable { get; set; }

    /// <summary>
    /// Describes how the streak status changed:
    /// "STARTED" | "ALREADY_COUNTED_TODAY" | "CONTINUED" | "FROZEN" | "BROKEN".
    /// </summary>
    public string StreakChanged { get; set; } = string.Empty;

    /// <summary>
    /// True if the current streak surpassed the previous all-time personal record during this activity.
    /// </summary>
    public bool IsNewRecord { get; set; }
}
