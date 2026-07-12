using HumanOS.Models.Capabilities;
using HumanOS.Models.Localization;

namespace HumanOS.Models.Practice;

public sealed class CapabilityPractice
{
    public Guid CapabilityPracticeId { get; set; }

    public Guid PersonCapabilityId { get; set; }

    public string PracticeType { get; set; } = null!;

    public int AssistanceLevel { get; set; }

    public string? PersonReflection { get; set; }

    public string LanguageCode { get; set; } = "en";

    public DateTime PracticedDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public PersonCapability PersonCapability { get; set; } = null!;

    public Language Language { get; set; } = null!;
}
