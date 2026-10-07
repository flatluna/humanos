namespace HumanOS.Contracts.Tenancy;

/// <summary>One row in the tenant totals' per-step breakdown — usage
/// summed across ALL of this tenant's courses (Capabilities + Structured
/// Learning Documents), grouped by StepName+Models (2026-10-01). Mirrors
/// <see cref="HumanOS.Contracts.Studio.StructuredLearningCostStepResponse"/>
/// but aggregated tenant-wide instead of per-document.</summary>
public sealed class TenantCourseTotalsStepResponse
{
    public string StepName { get; set; } = string.Empty;

    public string Models { get; set; } = string.Empty;

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long TotalElapsedMilliseconds { get; set; }

    public int ImagesGeneratedCount { get; set; }

    public int VideosGeneratedCount { get; set; }

    public decimal EstimatedCostUsd { get; set; }
}

/// <summary>Aggregate totals across every course this tenant has created
/// (2026-10-01) — powers the "Cursos totales" sub-tab, summing each
/// course's own cost-detail response (<see cref="HumanOS.Services.StructuredLearningCostService.GetDetailAsync"/>
/// / <see cref="HumanOS.Services.CapabilityCostService.GetDetailAsync"/>)
/// plus engram/chapter/fact counts.</summary>
public sealed class TenantCourseTotalsResponse
{
    public int CourseCount { get; set; }

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long TotalTokens => InputTokens + OutputTokens;

    public long TotalElapsedMilliseconds { get; set; }

    public int ImagesGeneratedCount { get; set; }

    public int VideosGeneratedCount { get; set; }

    public decimal EstimatedCostUsd { get; set; }

    public int EngramsCount { get; set; }

    public int ChaptersCount { get; set; }

    public int FactsCount { get; set; }

    public List<TenantCourseTotalsStepResponse> Steps { get; set; } = [];
}
