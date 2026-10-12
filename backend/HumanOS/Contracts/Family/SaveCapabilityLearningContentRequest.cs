namespace HumanOS.Contracts.Family;

public sealed class SaveCapabilityLearningContentRequest
{
    public Guid CapabilityId { get; set; }

    public string? StageKey { get; set; }

    public Guid? FamilyId { get; set; } // Null for global course template

    public string ModuleTitle { get; set; } = null!;

    public string PedagogicalGoal { get; set; } = null!;

    public string? KidExplanationStory { get; set; }

    public string? ParentGuide { get; set; }

    public List<string> HomeActivities { get; set; } = [];

    public List<string> SocraticQuestions { get; set; } = [];

    public string? MasteryRubric { get; set; }

    public bool IsPublished { get; set; } = true;
}
