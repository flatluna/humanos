namespace HumanOS.Models.Tenancy;

public sealed class Tenant
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Domain { get; set; }

    public string? Description { get; set; }

    public string CultureCode { get; set; } = "en-US";

    public string TimeZone { get; set; } = "UTC";

    public string? AzureTenantId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
