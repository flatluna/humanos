namespace HumanOS.Contracts.Tenancy;

/// <summary>One lab's AttemptEvaluationAgent prompt in/out usage/cost, for GET
/// /tenants/{tenantId}/lab-evaluation-costs \u2014 see
/// <see cref="HumanOS.Services.TenantLabEvaluationCostService"/>.</summary>
public sealed class TenantLabEvaluationCostResponse
{
    public Guid LabId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsGlobal { get; set; }

    public int EvaluationCount { get; set; }

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long CachedInputTokens { get; set; }

    public decimal EstimatedCostUsd { get; set; }
}

