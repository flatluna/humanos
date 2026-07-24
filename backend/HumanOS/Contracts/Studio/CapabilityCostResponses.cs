namespace HumanOS.Contracts.Studio;

/// <summary>
/// One card in the cost dashboard's list view — aggregated totals for a
/// single Capability. See <see cref="HumanOS.Services.CapabilityCostService"/>.
/// </summary>
public sealed class CapabilityCostSummaryResponse
{
    public Guid CapabilityId { get; set; }

    public string CapabilityName { get; set; } = string.Empty;

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long CachedInputTokens { get; set; }

    public long TotalTokens => InputTokens + OutputTokens;

    public int ImagesGeneratedCount { get; set; }

    public decimal EstimatedCostUsd { get; set; }

    /// <summary>See <see cref="HumanOS.Services.TokenCostEstimator"/>'s doc
    /// comment — always true today, rates are placeholders pending real
    /// confirmed Azure OpenAI pricing.</summary>
    public bool IsEstimate { get; set; } = true;

    /// <summary>UTC timestamp of this capability's earliest generation-usage
    /// row (i.e. when its generation pipeline started) — shown on the
    /// dashboard card and used for the day filter. Null if it has no
    /// persisted usage rows yet (2026-07-23).</summary>
    public DateTime? GeneratedDate { get; set; }
}

/// <summary>One row in the expanded card's per-section breakdown — usage
/// rows grouped by their <c>SectionLabel</c> (chapter/node name).</summary>
public sealed class CapabilityCostSectionResponse
{
    public string SectionLabel { get; set; } = string.Empty;

    /// <summary>Comma-separated distinct agent names that contributed to
    /// this section (e.g. "Curador" or "ExperienceDesigner, BlueprintValidator").</summary>
    public string Agents { get; set; } = string.Empty;

    /// <summary>Comma-separated distinct Azure OpenAI deployment names that
    /// served this section's calls (e.g. "gpt4mini" or "gpt-5-chat"). Empty
    /// for legacy usage rows that predate model-name tracking (2026-07-23).</summary>
    public string Models { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int CachedInputTokens { get; set; }

    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>This section's own slice of the estimated cost (per-model
    /// rates, no image cost — images are counted once at the capability
    /// level, not per section). See <see cref="HumanOS.Services.TokenCostEstimator"/>.</summary>
    public decimal EstimatedCostUsd { get; set; }
}

/// <summary>Full detail view for one Capability's expanded card.</summary>
public sealed class CapabilityCostDetailResponse
{
    public Guid CapabilityId { get; set; }

    public string CapabilityName { get; set; } = string.Empty;

    public List<CapabilityCostSectionResponse> Sections { get; set; } = [];

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long CachedInputTokens { get; set; }

    public long TotalTokens => InputTokens + OutputTokens;

    public int ImagesGeneratedCount { get; set; }

    public decimal EstimatedCostUsd { get; set; }

    public bool IsEstimate { get; set; } = true;
}
