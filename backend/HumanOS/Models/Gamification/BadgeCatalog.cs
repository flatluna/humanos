namespace HumanOS.Models.Gamification;

/// <summary>
/// Catalog of available gamification badges and their unlocking rules.
/// </summary>
public class BadgeCatalog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = null!;

    public string? Category { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? IconEmoji { get; set; }

    public string? RuleType { get; set; }

    public int RuleThreshold { get; set; }

    public bool IsActive { get; set; } = true;
}
