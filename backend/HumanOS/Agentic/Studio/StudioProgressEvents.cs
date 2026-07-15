using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Custom progress events for the Human OS Studio pipeline, raised via
/// <see cref="IWorkflowContext.AddEventAsync"/> from inside executors.
///
/// IMPORTANT: these do NOT pause the workflow (unlike <see cref="RequestInfoEvent"/>,
/// which IS how GATE 1 / GATE 2 human-in-the-loop pauses work — see
/// CapabilityCreationWorkflowFactory and the two RequestPort instances
/// there, both UNCHANGED by this file). AddEventAsync just surfaces an
/// informational event on the same <c>WatchStreamAsync()</c> stream the
/// orchestrator already drains — see CapabilityCreationOrchestrator,
/// which now runs that drain loop in the background and turns these
/// events into a pollable <see cref="CapabilityCreationRunStatus"/>
/// (Stage.Running) instead of blocking the HTTP request until the next
/// gate, matching the frontend's existing polling-based UI.
/// </summary>
public abstract class StudioProgressEvent : WorkflowEvent
{
    protected StudioProgressEvent(object? data) : base(data)
    {
    }
}

/// <summary>Raised once by ModuleQueueInitializerExecutor, right after
/// GATE 1 is approved, with the total module count so the frontend can
/// render "0 de N módulos" immediately.</summary>
public sealed class ModuleQueueStartedEvent : StudioProgressEvent
{
    public int TotalModules { get; }

    public ModuleQueueStartedEvent(int totalModules) : base(new { totalModules })
    {
        TotalModules = totalModules;
    }
}

/// <summary>Raised by InstructorExecutor right before it starts writing a
/// module's script (maps to the frontend's "GeneratingScript" module state).</summary>
public sealed class ModuleScriptStartedEvent : StudioProgressEvent
{
    public Guid ModuleId { get; }

    public string ModuleTitle { get; }

    public ModuleScriptStartedEvent(Guid moduleId, string moduleTitle)
        : base(new { moduleId, moduleTitle })
    {
        ModuleId = moduleId;
        ModuleTitle = moduleTitle;
    }
}

/// <summary>Raised by MetricoExecutor once a module's metrics have been
/// verified — i.e. the module is fully done (maps to the frontend's
/// "Verified" module state).</summary>
public sealed class ModuleVerifiedEvent : StudioProgressEvent
{
    public Guid ModuleId { get; }

    public string ModuleTitle { get; }

    public ModuleVerifiedEvent(Guid moduleId, string moduleTitle)
        : base(new { moduleId, moduleTitle })
    {
        ModuleId = moduleId;
        ModuleTitle = moduleTitle;
    }
}

/// <summary>
/// Raised by MetricoExecutor when <see cref="HumanOS.Agents.Studio.CompletedModuleValidator"/>
/// (Paso 5, 2026-07-14) finds the module's script well-formed but NOT
/// verified — a legitimate pedagogical outcome (e.g. Independence not
/// verified because the learner received a checklist), not a technical
/// error. Maps to the frontend's "Requiere revisión" module state (see
/// HUMAN-OS-STUDIO.md §14).
/// </summary>
public sealed class ModuleRequiresRevisionEvent : StudioProgressEvent
{
    public Guid ModuleId { get; }

    public string ModuleTitle { get; }

    public string Reason { get; }

    public ModuleRequiresRevisionEvent(Guid moduleId, string moduleTitle, string reason)
        : base(new { moduleId, moduleTitle, reason })
    {
        ModuleId = moduleId;
        ModuleTitle = moduleTitle;
        Reason = reason;
    }
}

/// <summary>
/// Raised by MetricoExecutor when a module fails
/// <see cref="HumanOS.Agents.Studio.CompletedModuleValidator"/>'s
/// STRUCTURAL checks (Paso 5, 2026-07-14) — a genuine bug/contract
/// violation between agents (e.g. TargetMetric changed, Recall not before
/// instruction), distinct from <see cref="ModuleRequiresRevisionEvent"/>'s
/// legitimate pedagogical outcome. Maps to the frontend's "Error" module
/// state (see HUMAN-OS-STUDIO.md §14).
/// </summary>
public sealed class ModuleProcessingFailedEvent : StudioProgressEvent
{
    public Guid ModuleId { get; }

    public string ModuleTitle { get; }

    public string Reason { get; }

    public ModuleProcessingFailedEvent(Guid moduleId, string moduleTitle, string reason)
        : base(new { moduleId, moduleTitle, reason })
    {
        ModuleId = moduleId;
        ModuleTitle = moduleTitle;
        Reason = reason;
    }
}

/// <summary>
/// Raised by <see cref="ModuleCompletionRouterExecutor"/> when a module
/// that did NOT reach <see cref="HumanOS.Agents.Studio.ModuleProcessingStatus.Verified"/>
/// is re-sent to the SAME Instructor agent for a bounded revision retry
/// (Paso 7, 2026-07-14 — see HUMAN-OS-STUDIO.md §16), instead of being
/// accepted as a final outcome. Maps to the frontend's "Reintentando"
/// module state.
/// </summary>
public sealed class ModuleRetryingEvent : StudioProgressEvent
{
    public Guid ModuleId { get; }

    public string ModuleTitle { get; }

    public int Attempt { get; }

    public string Feedback { get; }

    public ModuleRetryingEvent(Guid moduleId, string moduleTitle, int attempt, string feedback)
        : base(new { moduleId, moduleTitle, attempt, feedback })
    {
        ModuleId = moduleId;
        ModuleTitle = moduleTitle;
        Attempt = attempt;
        Feedback = feedback;
    }
}

/// <summary>
/// Raised by <see cref="ModuleCompletionRouterExecutor"/> exactly once per
/// module — when its outcome is accepted as FINAL (Verified, or
/// RequiresRevision/Failed with retries exhausted), never on an attempt
/// that is about to be retried (Paso 7, 2026-07-14 — see
/// HUMAN-OS-STUDIO.md §16). This is the ONLY event
/// <see cref="CapabilityCreationOrchestrator"/> uses to advance the
/// "N de Total módulos procesados" progress counter — using
/// <see cref="ModuleVerifiedEvent"/>/<see cref="ModuleRequiresRevisionEvent"/>/
/// <see cref="ModuleProcessingFailedEvent"/> directly for that would
/// double/triple-count a module that gets retried, since those fire once
/// per ATTEMPT, not once per module.
/// </summary>
public sealed class ModuleFinalizedEvent : StudioProgressEvent
{
    public Guid ModuleId { get; }

    public string ModuleTitle { get; }

    public ModuleFinalizedEvent(Guid moduleId, string moduleTitle)
        : base(new { moduleId, moduleTitle })
    {
        ModuleId = moduleId;
        ModuleTitle = moduleTitle;
    }
}

/// <summary>
/// Raised by PublishExecutor for each of the 6 publish sub-steps the
/// frontend already renders as a checklist (Capability/Levels/Modules/
/// Metrics/KnowledgeChunks/Embeddings). "KnowledgeChunks" covers per-module
/// chunking+embedding; "Embeddings" covers the capability-wide
/// TutorKnowledgeBase overview chunking+embedding plus the final DB commit
/// — see the comment above PublishExecutor.PersistAsync for the exact
/// mapping (the real backend does chunking+embedding together per chunk,
/// unlike the mock's separate phases; this is the closest faithful split).
/// </summary>
public sealed class PublishTaskProgressEvent : StudioProgressEvent
{
    public string TaskKey { get; }

    public string Status { get; }

    public PublishTaskProgressEvent(string taskKey, string status)
        : base(new { taskKey, status })
    {
        TaskKey = taskKey;
        Status = status;
    }
}
