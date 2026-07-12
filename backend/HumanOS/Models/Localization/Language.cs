using HumanOS.Models.Capabilities;
using HumanOS.Models.Goals;
using HumanOS.Models.People;
using HumanOS.Models.Practice;
using HumanOS.Models.Projects;
using HumanOS.Models.Recall;

namespace HumanOS.Models.Localization;

public sealed class Language
{
    public string LanguageCode { get; set; } = null!;

    public string EnglishName { get; set; } = null!;

    public string NativeName { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public ICollection<PersonProfile> PersonProfiles { get; set; } = [];

    public ICollection<CapabilityDomainTranslation>
        CapabilityDomainTranslations { get; set; } = [];

    public ICollection<CapabilityTranslation>
        CapabilityTranslations { get; set; } = [];

    public ICollection<GoalTranslation>
        GoalTranslations { get; set; } = [];

    public ICollection<ProjectTranslation>
        ProjectTranslations { get; set; } = [];

    public ICollection<CapabilityPractice>
        CapabilityPractices { get; set; } = [];

    public ICollection<RecallAttempt>
        RecallAttempts { get; set; } = [];
}
