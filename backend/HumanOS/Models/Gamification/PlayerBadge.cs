namespace HumanOS.Models.Gamification;

/// <summary>
/// Record of a badge unlocked by a student.
/// </summary>
public class PlayerBadge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PersonId { get; set; }

    public string BadgeCode { get; set; } = null!;

    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
}
