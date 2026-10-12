namespace HumanOS.Contracts.Family;

public sealed class FamilyMasterResponse
{
    public Guid FamilyId { get; set; }

    public string FamilyName { get; set; } = null!;

    public string FamilySlug { get; set; } = null!;

    public string HouseholdType { get; set; } = null!;

    public string AdminFullName { get; set; } = null!;

    public string AdminEmail { get; set; } = null!;

    public string? AdminPhone { get; set; }

    public string? CoAdminFullName { get; set; }

    public string? PrimaryCity { get; set; }

    public string? PrimaryState { get; set; }

    public string PrimaryCountry { get; set; } = null!;

    public string? SecondaryLocation { get; set; }

    public string TimeZone { get; set; } = null!;

    public string PreferredCurrency { get; set; } = null!;

    public string PrimaryLanguage { get; set; } = null!;

    public bool IsBilingualHousehold { get; set; }

    public decimal? AnnualEducationBudget { get; set; }

    public string SubscriptionTier { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }
}
