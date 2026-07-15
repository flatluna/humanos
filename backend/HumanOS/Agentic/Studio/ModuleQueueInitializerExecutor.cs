using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// First half of the sequential module loop (see
/// /memories/repo/humanstudio-multiagent-vision.md: "SECUENCIAL módulo por
/// módulo al inicio... deja el fan-out/fan-in preparado, actívalo
/// después"). Fires once per approved blueprint: stores the module queue
/// in shared workflow state and emits the FIRST module as work for the
/// Instructor. <see cref="ModuleCompletionRouterExecutor"/> emits the rest
/// one at a time as each module finishes the Instructor -&gt; Métrico
/// steps. Instructor and Métrico remain separate executors precisely so
/// this loop can later become a real fan-out/fan-in
/// (AddFanOutEdge/AddFanInBarrierEdge) without touching the agents
/// themselves.
/// </summary>
internal sealed class ModuleQueueInitializerExecutor : Executor<Gate1Outcome, ModuleRouterOutput>
{
    /// <summary>Shared-state scope name for the per-blueprint module
    /// queue, read/written by both module-router executors.</summary>
    internal const string ModuleQueueStateScope = "StudioModuleQueueState";

    public ModuleQueueInitializerExecutor() : base("ModuleQueueInitializer")
    {
    }

    public override async ValueTask<ModuleRouterOutput> HandleAsync(
        Gate1Outcome outcome,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var blueprint = outcome.ApprovedBlueprint
            ?? throw new ArgumentException(
                "ModuleQueueInitializerExecutor only handles routed messages with ApprovedBlueprint set.");

        var blueprintId = blueprint.BlueprintId;
        var queueState = new ModuleQueueState
        {
            Pending =
            [
                .. blueprint.Levels.SelectMany(level =>
                    level.Modules.Select(module => new PendingModuleRef { Layer = level.Layer, Module = module }))
            ]
        };

        // Progress event only (does not pause the workflow) — lets the
        // frontend render "0 de N módulos" as soon as generation starts.
        await context.AddEventAsync(new ModuleQueueStartedEvent(queueState.Pending.Count), cancellationToken);

        var next = DequeueNext(queueState);

        await context.QueueStateUpdateAsync(blueprintId.ToString(), queueState, scopeName: ModuleQueueStateScope);

        return next is null
            ? new ModuleRouterOutput
            {
                Completed = new AllModulesCompleted { BlueprintId = blueprintId, Modules = [] }
            }
            : new ModuleRouterOutput
            {
                NextModule = new ModuleWorkItem { BlueprintId = blueprintId, Layer = next.Layer, Module = next.Module }
            };
    }

    /// <summary>Pops and returns the next pending module from
    /// <paramref name="state"/>, or <c>null</c> if none remain. Shared
    /// with <see cref="ModuleCompletionRouterExecutor"/> so both halves of
    /// the loop dequeue identically.</summary>
    internal static PendingModuleRef? DequeueNext(ModuleQueueState state)
    {
        if (state.Pending.Count == 0)
        {
            return null;
        }

        var next = state.Pending[0];
        state.Pending.RemoveAt(0);
        return next;
    }
}
