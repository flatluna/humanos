namespace HumanOS.Contracts.Tenants;

public sealed class UpdateTenantRequest
{
    public string Name { get; set; } = null!;

    public string? Domain { get; set; }

    public string? Description { get; set; }

    public string CultureCode { get; set; } = "en-US";

    public string TimeZone { get; set; } = "UTC";

    public bool IsActive { get; set; } = true;
}
