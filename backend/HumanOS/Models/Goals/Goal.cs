namespace HumanOS.Models.Goals;

public sealed class Goal
{
    public Guid GoalId { get; set; }

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
