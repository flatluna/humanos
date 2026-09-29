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

    /// <summary>Videos generated for this document (2026-08-26) — priced
    /// separately from ImagesGeneratedCount since each video's real cost
    /// depends on which model made it (Veo $0.30 vs Gemini Omni Flash
    /// $0.55), not the flat per-image placeholder rate.</summary>
    public int VideosGeneratedCount { get; set; }

    public decimal VideoCostUsd { get; set; }

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

    public int VideosGeneratedCount { get; set; }

    public decimal VideoCostUsd { get; set; }

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

    public int VideosGeneratedCount { get; set; }

    public decimal VideoCostUsd { get; set; }

    public decimal EstimatedCostUsd { get; set; }

    public bool IsEstimate { get; set; } = true;
}

/// <summary>One AI model's share of an engram's cost — see
/// <see cref="HumanOS.Services.TokenCostEstimator.EstimateByModel"/>.</summary>
public sealed class StructuredLearningEngramModelCostResponse
{
    public string ModelName { get; set; } = string.Empty;

    public int CallCount { get; set; }

    public long InputTokens { get; set; }

    public long CachedInputTokens { get; set; }

    public long OutputTokens { get; set; }

    public decimal InputRatePerMillionTokens { get; set; }

    public decimal CachedInputRatePerMillionTokens { get; set; }

    public decimal OutputRatePerMillionTokens { get; set; }

    public decimal InputCostUsd { get; set; }

    public decimal CachedInputCostUsd { get; set; }

    public decimal OutputCostUsd { get; set; }

    public decimal TotalCostUsd { get; set; }
}

/// <summary>One video model's share of an engram's video cost (Veo vs Omni
/// Flash are priced per-video, not per-token).</summary>
public sealed class StructuredLearningEngramVideoModelCostResponse
{
    public string ModelName { get; set; } = string.Empty;

    public int VideoCount { get; set; }

    public decimal RatePerVideoUsd { get; set; }

    public decimal TotalCostUsd { get; set; }
}

/// <summary>Per-engram (per-section) cost breakdown row — everything that
/// went into ONE engram: initial design generation AND every subsequent
/// edit (manual or AI, both of which trigger real LLM calls for
/// audio/Knowledge Graph/Knowledge Facts resync). See
/// <see cref="HumanOS.Services.StructuredLearningCostService.GetPerEngramAsync"/>.</summary>
public sealed class StructuredLearningEngramCostResponse
{
    public Guid StructuredLearningDocumentSectionId { get; set; }

    public string ChapterTitle { get; set; } = string.Empty;

    public string SubchapterTitle { get; set; } = string.Empty;

    /// <summary>Distinct StepName values that contributed to this engram
    /// ("Extraction"/"KnowledgeGraph"/"KnowledgeFacts"/"Pedagogy"/"Edit").</summary>
    public string Steps { get; set; } = string.Empty;

    /// <summary>Total number of individual LLM/embedding calls billed to this engram.</summary>
    public int CallCount { get; set; }

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long CachedInputTokens { get; set; }

    public long TotalTokens => InputTokens + OutputTokens;

    /// <summary>Per-model breakdown of the LLM cost above — each model's own
    /// input/output/cached rate per million tokens and resulting cost.</summary>
    public List<StructuredLearningEngramModelCostResponse> ModelBreakdown { get; set; } = [];

    public decimal LlmCostUsd { get; set; }

    public int ImagesGeneratedCount { get; set; }

    public decimal ImageRatePerImageUsd { get; set; }

    public decimal ImageCostUsd { get; set; }

    public int VideosGeneratedCount { get; set; }

    /// <summary>Per-video-model breakdown (Veo 3.1 Fast $0.30/video vs
    /// Gemini Omni Flash $0.55/video) — videos are priced per-video, not
    /// per-token, so this is separate from ModelBreakdown above.</summary>
    public List<StructuredLearningEngramVideoModelCostResponse> VideoModelBreakdown { get; set; } = [];

    public decimal VideoCostUsd { get; set; }

    /// <summary>LlmCostUsd + ImageCostUsd + VideoCostUsd — the real all-in
    /// cost of this single engram, usable to bill it to a client.</summary>
    public decimal TotalCostUsd { get; set; }

    public bool IsEstimate { get; set; } = true;
}
