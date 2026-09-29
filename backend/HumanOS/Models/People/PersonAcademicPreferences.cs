namespace HumanOS.Models.People;

/// <summary>
/// Latest academic interests and learning preferences captured for one person.
/// The selected values are stored as JSON arrays so the form can support
/// multiple choices per category without creating a row for every option.
/// </summary>
public sealed class PersonAcademicPreferences
{
    public Guid PersonAcademicPreferencesId { get; set; }

    public Guid PersonId { get; set; }

    public string AcademicInterestsJson { get; set; } = "[]";

    public string LearningPreferencesJson { get; set; } = "[]";

    public string AcademicMotivationsJson { get; set; } = "[]";

    public string GrowthAreasJson { get; set; } = "[]";

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public Person Person { get; set; } = null!;
}
