namespace HumanOS.Contracts.Gamification;

/// <summary>
/// Represents a badge awarded to a student upon fulfilling its achievement rule.
/// </summary>
public class AwardedBadge
{
    public string BadgeCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string IconEmoji { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime EarnedAt { get; set; }
}
