namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// Persisted per-agent-call token usage + elapsed time for ONE Structured
/// Learning Document pipeline step (Extraction/embedding per section,
/// Knowledge Graph entity extraction per section, Pedagogy design per
/// section) — mirrors <see cref="HumanOS.Models.Capabilities.Graph.CapabilityGenerationUsage"/>'s
/// exact design, adapted to this pipeline's own step names. One row = one
/// LLM/embedding call. Backing store for a future cost-per-document
/// dashboard; not yet surfaced in the UI.
/// </summary>
public sealed class StructuredLearningGenerationUsage
{
    public Guid StructuredLearningGenerationUsageId { get; set; }

    public Guid StructuredLearningDocumentId { get; set; }

    /// <summary>Which section this call was for — null for document-wide
    /// calls (e.g. the executive summary).</summary>
    public Guid? StructuredLearningDocumentSectionId { get; set; }

    /// <summary>"Extraction" / "KnowledgeGraph" / "Pedagogy" / "ExecutiveSummary" — which pipeline step this call belongs to.</summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>Which agent/service made this call (e.g.
    /// "StructuredLearningEntityExtractionAgent", "StructuredLearningSectionDesigner").</summary>
    public string AgentName { get; set; } = string.Empty;

    public string? ModelName { get; set; }

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int CachedInputTokens { get; set; }

    public long ElapsedMilliseconds { get; set; }

    public DateTime CreatedDate { get; set; }
}
