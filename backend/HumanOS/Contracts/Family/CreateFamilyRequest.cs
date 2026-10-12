namespace HumanOS.Contracts.Family;

public sealed class CreateFamilyRequest
{
    public string FamilyName { get; set; } = null!;

    public string AdminFirstName { get; set; } = null!;

    public string AdminLastName { get; set; } = null!;

    public string AdminEmail { get; set; } = null!;

    public string? AdminPhone { get; set; }

    public string? CoAdminFullName { get; set; }

    public string? PrimaryCity { get; set; }

    public string? PrimaryState { get; set; }

    public string? PrimaryCountry { get; set; } = "USA";

    public string? SecondaryLocation { get; set; }

    public string? TimeZone { get; set; } = "America/Chicago";

    public string? PreferredCurrency { get; set; } = "USD";

    public string? PrimaryLanguage { get; set; } = "es";

    public bool IsBilingualHousehold { get; set; } = true;

    public decimal? AnnualEducationBudget { get; set; }
}
