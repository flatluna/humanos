using HumanOS.Models.Localization;

namespace HumanOS.Models.Motivations;

public sealed class MotivationTranslation
{
    public Guid MotivationId { get; set; }

    public string LanguageCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public Motivation Motivation { get; set; } = null!;

    public Language Language { get; set; } = null!;
}
