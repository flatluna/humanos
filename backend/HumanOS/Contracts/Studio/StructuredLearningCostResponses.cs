namespace HumanOS.Contracts.Studio;

/// <summary>List view row: aggregated usage totals for one Structured
/// Learning Document. See <see cref="HumanOS.Services.StructuredLearningCostService"/>.</summary>
public sealed class StructuredLearningCostSummaryResponse
{
    public Guid StructuredLearningDocumentId { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long CachedInputTokens { get; set; }

    public long TotalTokens => InputTokens + OutputTokens;

    public long TotalElapsedMilliseconds { get; set; }

    public int ImagesGeneratedCount { get; set; }

    public decimal ImageCostUsd { get; set; }

    public decimal EstimatedCostUsd { get; set; }

    public bool IsEstimate { get; set; } = true;

    public DateTime? GeneratedDate { get; set; }
}

/// <summary>One row in the detail view's per-step breakdown — usage rows
/// grouped by <c>StepName</c> ("Extraction"/"KnowledgeGraph"/"Pedagogy").</summary>
public sealed class StructuredLearningCostStepResponse
{
    public string StepName { get; set; } = string.Empty;

    /// <summary>Comma-separated distinct agent names that contributed to this step.</summary>
    public string Agents { get; set; } = string.Empty;

    public string Models { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int CachedInputTokens { get; set; }

    public int TotalTokens => InputTokens + OutputTokens;

    public long TotalElapsedMilliseconds { get; set; }

    /// <summary>Number of calls that make up this step's totals — useful
    /// alongside TotalElapsedMilliseconds to see the average call time.</summary>
    public int CallCount { get; set; }

    public int ImagesGeneratedCount { get; set; }

    public decimal ImageCostUsd { get; set; }

    public decimal EstimatedCostUsd { get; set; }
}

/// <summary>Full detail view for one Structured Learning Document.</summary>
public sealed class StructuredLearningCostDetailResponse
{
    public Guid StructuredLearningDocumentId { get; set; }

    public string DocumentName { get; set; } = string.Empty;

    public List<StructuredLearningCostStepResponse> Steps { get; set; } = [];

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long CachedInputTokens { get; set; }

    public long TotalTokens => InputTokens + OutputTokens;

    public long TotalElapsedMilliseconds { get; set; }

    public int ImagesGeneratedCount { get; set; }

    public decimal ImageCostUsd { get; set; }

    public decimal EstimatedCostUsd { get; set; }

    public bool IsEstimate { get; set; } = true;
}
