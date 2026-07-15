namespace HumanOS.Contracts.People;

public sealed class UpsertPersonProfileRequest
{
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
}
