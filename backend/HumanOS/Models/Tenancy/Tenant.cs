namespace HumanOS.Models.Tenancy;

public sealed class Tenant
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Domain { get; set; }

    public string? Description { get; set; }

    /// <summary>Company address, collected during onboarding.</summary>
    public string? Address { get; set; }

    /// <summary>Company contact email, collected during onboarding —
    /// distinct from any individual Person's email.</summary>
    public string? Email { get; set; }

    /// <summary>Company contact phone, collected during onboarding.</summary>
    public string? Phone { get; set; }

    public string CultureCode { get; set; } = "en-US";

    public string TimeZone { get; set; } = "UTC";

    public string? AzureTenantId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
