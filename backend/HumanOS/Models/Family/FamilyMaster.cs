namespace HumanOS.Models.Family;

/// <summary>
/// Master record for a Family Household (Tenant equivalent in the Family ecosystem).
/// Completely independent and additive — does not modify existing corporate Tenant models.
/// </summary>
public sealed class FamilyMaster
{
    public Guid FamilyId { get; set; } = Guid.NewGuid();

    public string FamilyName { get; set; } = null!;

    public string FamilySlug { get; set; } = null!;

    public string HouseholdType { get; set; } = "Bicultural";

    // Primary Family Administrator
    public string AdminFullName { get; set; } = null!;

    public string AdminEmail { get; set; } = null!;

    public string? AdminPhone { get; set; }

    public string? AdminAzureOid { get; set; }

    // Co-Administrator (Spouse / Secondary Guardian)
    public string? CoAdminFullName { get; set; }

    public string? CoAdminEmail { get; set; }

    // Primary Physical Address
    public string? PrimaryStreet { get; set; }

    public string? PrimaryCity { get; set; }

    public string? PrimaryState { get; set; }

    public string? PrimaryZipCode { get; set; }

    public string PrimaryCountry { get; set; } = "USA";

    // Secondary Location (e.g., Mexico City for bicultural/cross-border households)
    public string? SecondaryLocation { get; set; }

    // Regional & Bicultural Settings
    public string TimeZone { get; set; } = "America/Chicago";

    public string PreferredCurrency { get; set; } = "USD";

    public string PrimaryLanguage { get; set; } = "es";

    public bool IsBilingualHousehold { get; set; } = true;

    // Household Wellness & Finances
    public decimal? AnnualEducationBudget { get; set; }

    public TimeSpan? DigitalSunsetTime { get; set; } = new TimeSpan(21, 0, 0);

    public string? FamilyMissionStatement { get; set; }

    public string SubscriptionTier { get; set; } = "FamilyEnterprise";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<FamilyMember> Members { get; set; } = [];
}
