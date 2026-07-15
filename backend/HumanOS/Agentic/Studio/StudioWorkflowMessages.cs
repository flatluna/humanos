using HumanOS.Agents.Studio;

namespace HumanOS.Agentic.Studio;

/// <summary>Kicks off a capability-creation run: the raw material the user
/// feeds in, plus the capability goal.</summary>
public sealed class RawMaterialBatch
{
    /// <summary>The pre-existing CapabilityDomain (fixed taxonomy: Mind,
    /// Build, Home, Life, Value, Future) the published Capability will
    /// belong to. Required — Studio does not invent domains.</summary>
    public Guid CapabilityDomainId { get; set; }

    public string CapabilityGoal { get; set; } = string.Empty;

    public List<RawMaterialItem> Materials { get; set; } = [];
}

/// <summary>Curador's output, carrying the capability goal and domain forward to Arquitecto.</summary>
internal sealed class CuratorOutput
{
    public Guid CapabilityDomainId { get; set; }

    public string CapabilityGoal { get; set; } = string.Empty;

    public CuratedCorpus Corpus { get; set; } = null!;

    /// <summary>Token usage for the Curador call that produced <see cref="Corpus"/>
    /// (observability only, see <see cref="AgentTokenUsage"/>).</summary>
    public AgentTokenUsage TokenUsage { get; set; } = null!;
}

/// <summary>One module handed to the Instructor, in the context of the
/// blueprint and level it belongs to.</summary>
internal sealed class ModuleWorkItem
{
    public Guid BlueprintId { get; set; }

    public HumanEvolutionLayer Layer { get; set; }

    public ModuleSkeleton Module { get; set; } = null!;

    /// <summary>0 on the first attempt; incremented by
    /// <see cref="ModuleCompletionRouterExecutor"/> each time this SAME
    /// module is re-sent to the Instructor for a bounded revision retry
    /// (Paso 7, 2026-07-14 — see HUMAN-OS-STUDIO.md §16).</summary>
    public int Attempt { get; set; }

    /// <summary>Set only on a revision retry (Attempt &gt; 0) — the
    /// previous rejected script + Métrico's specific feedback, passed to
    /// <see cref="HumanOS.Agents.Studio.InstructorAgent.WriteScriptAsync"/>.</summary>
    public RevisionContext? Revision { get; set; }
}

/// <summary>A module's script, still carrying its work-item context, on
/// its way to the Métrico agent.</summary>
internal sealed class ModuleScriptWorkItem
{
    public ModuleWorkItem Item { get; set; } = null!;

    public ModuleScript Script { get; set; } = null!;
}

/// <summary>Emitted by the module router once every module in the
/// blueprint has completed the Instructor -&gt; Métrico steps.</summary>
internal sealed class AllModulesCompleted
{
    public Guid BlueprintId { get; set; }

    public List<CompletedModule> Modules { get; set; } = [];
}

/// <summary>One not-yet-processed module, tracked in shared workflow state
/// while the sequential module loop runs.</summary>
internal sealed class PendingModuleRef
{
    public HumanEvolutionLayer Layer { get; set; }

    public ModuleSkeleton Module { get; set; } = null!;
}

/// <summary>Shared-state record for the sequential module loop (see
/// ModuleQueueInitializerExecutor / ModuleCompletionRouterExecutor), keyed
/// by BlueprintId.</summary>
internal sealed class ModuleQueueState
{
    public List<PendingModuleRef> Pending { get; set; } = [];

    public List<CompletedModule> Completed { get; set; } = [];
}

/// <summary>Metrico's output, still carrying the BlueprintId so the module
/// router knows which queue in shared state to advance.</summary>
internal sealed class CompletedModuleResult
{
    public Guid BlueprintId { get; set; }

    /// <summary>Echoed from the originating <see cref="ModuleWorkItem"/> so
    /// <see cref="ModuleCompletionRouterExecutor"/> can re-send the SAME
    /// module for a retry without needing to look up its level elsewhere.</summary>
    public HumanEvolutionLayer Layer { get; set; }

    /// <summary>Echoed from the originating <see cref="ModuleWorkItem"/> —
    /// how many revision retries this module has already had (0 on the
    /// first attempt).</summary>
    public int Attempt { get; set; }

    public CompletedModule Completed { get; set; } = null!;
}

/// <summary>
/// Common output type for the two module-router executors
/// (ModuleQueueInitializerExecutor, ModuleCompletionRouterExecutor).
/// Exactly one of the two properties is set; downstream conditional edges
/// route to InstructorExecutor (NextModule) or ExperienciaExecutor
/// (Completed) accordingly — same pattern as the Agent Framework's
/// conditional-edges spam-detection sample.
/// </summary>
internal sealed class ModuleRouterOutput
{
    public ModuleWorkItem? NextModule { get; set; }

    public AllModulesCompleted? Completed { get; set; }
}

/// <summary>
/// Terminal output when module generation finishes but NOT every module
/// reached <see cref="ModuleProcessingStatus.Verified"/> (fixed Paso 5,
/// 2026-07-14 — see HUMAN-OS-STUDIO.md §14). Yielded by
/// <see cref="ModuleRevisionRequiredExecutor"/> instead of proceeding to
/// Experiencia/Gate 2/Publish — carries every module (Verified,
/// RequiresRevision, and Failed alike) so the caller can see exactly what
/// needs attention.
/// </summary>
public sealed class ModuleGenerationOutcome
{
    public Guid BlueprintId { get; set; }

    public List<CompletedModule> Modules { get; set; } = [];
}

/// <summary>Shared state persisted across the workflow's RequestPort gates
/// (see ArquitectoExecutor.PipelineStateScope), keyed by BlueprintId, so
/// downstream executors can retrieve the blueprint + curated corpus
/// without threading large payloads through every edge.</summary>
internal sealed class PipelineState
{
    public Guid CapabilityDomainId { get; set; }

    public CapabilityBlueprint Blueprint { get; set; } = null!;

    public CuratedCorpus CuratedCorpus { get; set; } = null!;

    /// <summary>
    /// Per-agent-call token usage recorded across the run (Paso 3,
    /// 2026-07-14 — secondary/observability concern only, see
    /// HUMAN-OS-STUDIO.md §12). Currently only the Instructor appends to
    /// this (one entry per module script written).
    /// </summary>
    public List<AgentTokenUsage> TokenUsage { get; set; } = [];
}

/// <summary>
/// Gate1DecisionExecutor's output. Exactly one property is set; downstream
/// conditional edges route to ModuleQueueInitializerExecutor
/// (ApprovedBlueprint) or Gate1RejectionExecutor (RejectionMessage).
/// A single-output-type executor (<see cref="Executor{TInput,TOutput}"/>)
/// can only ever "send" its declared TOutput type — it cannot conditionally
/// send a different type (e.g. the raw <see cref="CapabilityBlueprint"/>)
/// from inside the handler, hence this wrapper + conditional-edge pattern
/// (same technique as <see cref="ModuleRouterOutput"/>).
/// </summary>
internal sealed class Gate1Outcome
{
    public CapabilityBlueprint? ApprovedBlueprint { get; set; }

    public string? RejectionMessage { get; set; }
}

/// <summary>Gate2DecisionExecutor's output — same pattern as <see cref="Gate1Outcome"/>.</summary>
internal sealed class Gate2Outcome
{
    public CapabilityPackage? ApprovedPackage { get; set; }

    public string? RejectionMessage { get; set; }
}

/// <summary>
/// A human reviewer's response to GATE 1 (after Arquitecto) or GATE 2
/// (before publishing). <see cref="SubjectId"/> echoes the
/// CapabilityBlueprint.BlueprintId (Gate 1) or CapabilityPackage.PackageId
/// (Gate 2) the reviewer is responding to.
/// </summary>
public sealed class GateDecision
{
    public Guid SubjectId { get; set; }

    public bool Approved { get; set; }

    public string? Comments { get; set; }

    /// <summary>
    /// Gate 1 only: an optional edited/reduced blueprint to use INSTEAD of
    /// the one Arquitecto originally produced (e.g. trimmed down to a
    /// couple of modules for a cheap smoke test). Its own
    /// <c>BlueprintId</c> is ignored — <see cref="SubjectId"/> (from the
    /// respond URL) always wins, since <c>BlueprintId</c> is
    /// <c>[JsonIgnore]</c>d and would just deserialize as a fresh GUID
    /// anyway. Ignored for Gate 2.
    /// </summary>
    public CapabilityBlueprint? RevisedBlueprint { get; set; }
}
