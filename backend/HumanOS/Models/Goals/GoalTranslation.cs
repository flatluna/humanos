using HumanOS.Models.Localization;

namespace HumanOS.Models.Goals;

public sealed class GoalTranslation
{
    public Guid GoalId { get; set; }

    public string LanguageCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public Goal Goal { get; set; } = null!;

    public Language Language { get; set; } = null!;
}
