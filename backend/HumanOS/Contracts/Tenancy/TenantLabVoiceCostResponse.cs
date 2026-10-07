namespace HumanOS.Contracts.Tenancy;

/// <summary>One lab's Realtime-voice agent usage/cost, for GET
/// /tenants/{tenantId}/lab-voice-costs \u2014 see
/// <see cref="HumanOS.Services.TenantLabVoiceCostService"/>.</summary>
public sealed class TenantLabVoiceCostResponse
{
    public Guid LabId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsGlobal { get; set; }

    public int SessionCount { get; set; }

    public int TotalDurationSeconds { get; set; }

    public decimal EstimatedCostUsd { get; set; }
}

