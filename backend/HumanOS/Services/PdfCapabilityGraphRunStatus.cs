using System.Text.Json.Serialization;
using HumanOS.Agents.Studio;

namespace HumanOS.Services;

/// <summary>
/// Status snapshot for one in-progress or finished PDF→CapabilityGraph run
/// (see <see cref="PdfCapabilityGraphOrchestrator"/>). Polled by the
/// frontend, same non-blocking pattern as
/// HumanOS.Agentic.Studio.CapabilityCreationOrchestrator's
/// CapabilityCreationRunStatus (V1).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PdfCapabilityGraphStage
{
    Running,
    Completed,
    Failed
}

public sealed class PdfCapabilityGraphRunStatus
{
    public Guid RunId { get; set; }

    public PdfCapabilityGraphStage Stage { get; set; }

    /// <summary>Short, human-readable description of what the pipeline is
    /// doing right now (e.g. "Curando capítulo 3 de 10"), for a simple
    /// progress indicator in the UI — not a strict state machine.</summary>
    public string CurrentStepDescription { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public PdfCapabilityGraphResult? Result { get; set; }

    public static PdfCapabilityGraphRunStatus Running(Guid runId, string step) => new()
    {
        RunId = runId,
        Stage = PdfCapabilityGraphStage.Running,
        CurrentStepDescription = step
    };

    public static PdfCapabilityGraphRunStatus Failed(Guid runId, string error) => new()
    {
        RunId = runId,
        Stage = PdfCapabilityGraphStage.Failed,
        ErrorMessage = error
    };

    public static PdfCapabilityGraphRunStatus Completed(Guid runId, PdfCapabilityGraphResult result) => new()
    {
        RunId = runId,
        Stage = PdfCapabilityGraphStage.Completed,
        CurrentStepDescription = "Completado",
        Result = result
    };
}

/// <summary>Final summary of a completed run — enough for the UI to link
/// straight into the newly created capability graph.</summary>
public sealed class PdfCapabilityGraphResult
{
    public Guid CapabilityId { get; set; }

    public Guid CapabilityGraphId { get; set; }

    /// <summary>Set when the caller asked to attach this newly created
    /// Capability to an existing Program's sequence (see StartRequest's
    /// optional ProgramId) — lets the frontend link straight back to the
    /// Program after generation completes.</summary>
    public Guid? ProgramId { get; set; }

    public string GraphName { get; set; } = string.Empty;

    public int PageCount { get; set; }

    public int ChapterCount { get; set; }

    public int NodeCount { get; set; }

    public int EdgeCount { get; set; }

    public int NodesWithBlueprintCount { get; set; }

    /// <summary>Deterministic (non-LLM) coverage check: curated-corpus chunk
    /// tags that no node ended up referencing in its <c>References</c> list
    /// — a signal (not a hard failure) that some chunk of source material
    /// may not have produced any node. Empty when every chunk was used by
    /// at least one node.</summary>
    public List<string> UnreferencedChunkTags { get; set; } = [];

    /// <summary>Per-LLM-call token usage for EVERY agent invocation in this
    /// run (2026-07-20 — cost-per-capability observability, see
    /// /memories/repo/humanstudio-multiagent-vision.md): one entry per
    /// CuradorAgent chapter call (ModuleId = chapter label), one for the
    /// single GraphArchitectAgent call, one for DocumentContextAgent (if
    /// configured/succeeded), and one ExperienceDesignerAgent + one
    /// BlueprintValidatorAgent entry PER node (ModuleId = node name).
    /// Illustration generation is NOT included here since gpt-image
    /// billing is per-image, not per-token — see
    /// <see cref="IllustrationsGeneratedCount"/> for that cost driver
    /// instead. Sum <see cref="AgentTokenUsage.TotalTokens"/> across this
    /// list (optionally grouped by <see cref="AgentTokenUsage.AgentName"/>)
    /// to get this capability's total LLM token cost.</summary>
    public List<AgentTokenUsage> TokenUsage { get; set; } = [];

    /// <summary>How many illustration images were actually generated (and
    /// uploaded) in this run — the OTHER cost driver besides
    /// <see cref="TokenUsage"/>, billed per-image rather than per-token.</summary>
    public int IllustrationsGeneratedCount { get; set; }

    /// <summary>How many images EMBEDDED in the source PDF's own pages
    /// (scanned pages, diagrams, photos) were described by
    /// <see cref="HumanOS.Agents.Studio.PdfImageDescriptionAgent"/> and
    /// folded into the material Curador/GraphArchitect saw (2026-07-23) —
    /// 0 for the description/idea entry point (no source PDF), and 0 if
    /// the agent isn't configured. Its per-call token usage is included in
    /// <see cref="TokenUsage"/> (AgentName "PdfImageDescription").</summary>
    public int DescribedImageCount { get; set; }

    /// <summary>Estimated USD cost of this run, derived from <see cref="TokenUsage"/>
    /// and <see cref="IllustrationsGeneratedCount"/> — see
    /// <see cref="TokenCostEstimator"/> for the placeholder-rate caveat.</summary>
    public PdfCapabilityCostEstimate? EstimatedCost { get; set; }
}
