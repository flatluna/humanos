using System;

namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// Persisted per-agent-call token usage for ONE Capability-generation
/// pipeline run (Curador per chapter, GraphArchitect, DocumentContext,
/// ExperienceDesigner per node, BlueprintValidator per node) — the SQL
/// backing store for the cost-per-capability dashboard (2026-07-23).
///
/// Before this, <c>AgentTokenUsage</c> was computed by every agent but only
/// ever lived in-memory (<c>PdfCapabilityGraphResult.TokenUsage</c>), visible
/// solely via the ephemeral run-status polling endpoint while
/// <c>PdfCapabilityGraphOrchestrator</c>'s in-memory run dictionary still
/// held it — lost forever once the run entry was evicted or the process
/// restarted. Persisting one row per agent call here means the cost of ANY
/// already-created Capability can be recomputed at any time, from a
/// dedicated dashboard, long after the run finished.
///
/// One row = one LLM call. <see cref="SectionLabel"/> identifies WHICH part
/// of the course that call was for (a chapter, the whole-graph design call,
/// or a specific node's blueprint/validation) — this is the "section" the
/// cost dashboard groups/displays by. Illustration (image) costs are NOT
/// tracked here (images aren't token-based) — the dashboard instead counts
/// existing <see cref="CapabilityGraphNodeIllustration"/> rows for the same
/// Capability.
/// </summary>
public class CapabilityGenerationUsage
{
    public Guid CapabilityGenerationUsageId { get; set; } = Guid.NewGuid();

    /// <summary>FK: Capability this LLM call was generating content for.</summary>
    public Guid CapabilityId { get; set; }

    /// <summary>Which CapabilityGraph generation run produced this row —
    /// a fresh CapabilityGraph is created every pipeline run, so grouping
    /// by this lets a re-generated Capability's cost history stay separable
    /// per run rather than being silently merged together.</summary>
    public Guid CapabilityGraphId { get; set; }

    /// <summary>Which agent made this call (e.g. "Curador", "GraphArchitect",
    /// "DocumentContext", "ExperienceDesigner", "BlueprintValidator").</summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>Azure OpenAI deployment/model name that actually served this
    /// call (e.g. "gpt4mini", "gpt-5-chat") — 2026-07-23, needed because
    /// different agents deliberately use different-cost models (see
    /// AgentTokenUsage.ModelName's doc comment). Null for rows persisted
    /// before this field existed.</summary>
    public string? ModelName { get; set; }

    /// <summary>Human-readable section this call belongs to — a chapter
    /// label ("Cap.1 Introducción"), the capability name (for the single
    /// GraphArchitect/DocumentContext calls), or a node name (for
    /// per-node ExperienceDesigner/BlueprintValidator calls).</summary>
    public string? SectionLabel { get; set; }

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    /// <summary>Already included in <see cref="InputTokens"/> — see
    /// <c>AgentTokenUsage.CachedInputTokens</c>'s own doc comment.</summary>
    public int CachedInputTokens { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    public virtual Capability? Capability { get; set; }
}
