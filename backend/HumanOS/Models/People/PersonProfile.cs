using HumanOS.Models.JobDescriptions;
using HumanOS.Models.Localization;

namespace HumanOS.Models.People;

public sealed class PersonProfile
{
    public Guid PersonProfileId { get; set; }

    public Guid PersonId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? DisplayName { get; set; }

    public string? PhoneNumber { get; set; }

    public string PreferredLanguage { get; set; } = "en";

    public string? CountryCode { get; set; }

    public string? TimeZone { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Occupation { get; set; }

    public string? Company { get; set; }

    public string? Biography { get; set; }

    /// <summary>Pointer to the employee's current, employee-confirmed
    /// <see cref="JobDescriptionRecord"/> (see
    /// JobDescriptionRecord.ExtractionStatus == "Confirmed"). Null until
    /// the employee has reviewed and confirmed an extracted Job
    /// Description — never set automatically from a raw extraction.
    /// </summary>
    public Guid? CurrentJobDescriptionId { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Person Person { get; set; } = null!;

    public Language? PreferredLanguageData { get; set; }

    public JobDescriptionRecord? CurrentJobDescription { get; set; }
}
