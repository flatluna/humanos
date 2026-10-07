namespace HumanOS.Contracts.Tenancy;

/// <summary>Response for GET /tenants/{tenantId}/buddy-usage — see
/// <see cref="HumanOS.Services.TenantBuddyUsageService"/>.</summary>
public sealed class TenantBuddyUsageResponse
{
    public int SessionCount { get; set; }

    public int TotalDurationSeconds { get; set; }

    public decimal EstimatedCostUsd { get; set; }
}
