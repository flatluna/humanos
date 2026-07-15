using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Second half of the sequential module loop (see
/// ModuleQueueInitializerExecutor). Fires once per completed module: if
/// the module did NOT reach Verified and still has retries left (Paso 7,
/// 2026-07-14 — see HUMAN-OS-STUDIO.md §16), re-sends the SAME module to
/// the SAME Instructor agent with Métrico's specific feedback attached
/// (see <see cref="RevisionContext"/>) instead of accepting the outcome as
/// final. Otherwise, records the result in shared state and either emits
/// the next module as work for the Instructor, or — once all modules are
/// done — emits the full <see cref="AllModulesCompleted"/> result for
/// Experiencia.
/// </summary>
internal sealed class ModuleCompletionRouterExecutor : Executor<CompletedModuleResult, ModuleRouterOutput>
{
    /// <summary>
    /// Max number of revision retries per module (so up to 1 + MaxRetries
    /// total Instructor attempts before a non-Verified outcome is accepted
    /// as final). Bounded deliberately: this reuses the SAME Instructor
    /// agent/prompt with Métrico's specific feedback appended — it never
    /// relaxes what "Verified" means for any individual module (that bar
    /// is still decided per-criterion by CompletedModuleValidator/Métrico;
    /// only the capability-level publish threshold in
    /// <see cref="ModuleCompletionGate"/> is more lenient).
    /// </summary>
    internal const int MaxRetries = 2;

    public ModuleCompletionRouterExecutor() : base("ModuleCompletionRouter")
    {
    }

    public override async ValueTask<ModuleRouterOutput> HandleAsync(
        CompletedModuleResult input,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (input.Completed.Status != ModuleProcessingStatus.Verified && input.Attempt < MaxRetries)
        {
            var feedback = input.Completed.Status == ModuleProcessingStatus.Failed
                ? input.Completed.FailureReason ?? "Structural contract violation reported with no message."
                : input.Completed.Metrics.Rationale;

            await context.AddEventAsync(
                new ModuleRetryingEvent(
                    input.Completed.Module.ModuleId, input.Completed.Module.Title, input.Attempt + 1, feedback),
                cancellationToken);

            return new ModuleRouterOutput
            {
                NextModule = new ModuleWorkItem
                {
                    BlueprintId = input.BlueprintId,
                    Layer = input.Layer,
                    Module = input.Completed.Module,
                    Attempt = input.Attempt + 1,
                    Revision = new RevisionContext
                    {
                        PreviousScript = input.Completed.Script,
                        Feedback = feedback
                    }
                }
            };
        }

        // This outcome is FINAL (Verified, or retries exhausted) — count it
        // toward progress exactly once here (see ModuleFinalizedEvent),
        // never from the per-attempt Metrico events directly, so a retried
        // module isn't double/triple-counted.
        await context.AddEventAsync(
            new ModuleFinalizedEvent(input.Completed.Module.ModuleId, input.Completed.Module.Title),
            cancellationToken);

        var queueState = await context.ReadStateAsync<ModuleQueueState>(
            input.BlueprintId.ToString(),
            scopeName: ModuleQueueInitializerExecutor.ModuleQueueStateScope) ?? new ModuleQueueState();

        queueState.Completed.Add(input.Completed);
        var next = ModuleQueueInitializerExecutor.DequeueNext(queueState);

        await context.QueueStateUpdateAsync(
            input.BlueprintId.ToString(),
            queueState,
            scopeName: ModuleQueueInitializerExecutor.ModuleQueueStateScope);

        return next is null
            ? new ModuleRouterOutput
            {
                Completed = new AllModulesCompleted { BlueprintId = input.BlueprintId, Modules = queueState.Completed }
            }
            : new ModuleRouterOutput
            {
                NextModule = new ModuleWorkItem { BlueprintId = input.BlueprintId, Layer = next.Layer, Module = next.Module }
            };
    }
}
