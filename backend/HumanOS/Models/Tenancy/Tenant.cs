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

    /// <summary>"Corporate" (default) or "Academic" — drives the employee
    /// portal's branding/presentation (logo, header copy, whether the
    /// student-only "Tec Contigo" section is shown) at the tenant level,
    /// since a whole school tenant is students and a whole company tenant
    /// is employees, never a per-person mix.</summary>
    public string OrgType { get; set; } = "Corporate";

    public string? AzureTenantId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
