namespace HumanOS.Models.Goals;

public sealed class Goal
{
    public Guid GoalId { get; set; }

    /// <summary>Stable slug (e.g. "helpFamily") used to match a catalog row
    /// from client code, independent of the localized display Name. Same
    /// pattern as <see cref="HumanOS.Models.Capabilities.Subject.Code"/>.</summary>
    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public ICollection<GoalTranslation> Translations { get; set; } = [];

    public ICollection<PersonGoal> PersonGoals { get; set; } = [];

    public ICollection<GoalCapability> GoalCapabilities { get; set; } = [];
}
