using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Terminal step when module generation finishes but NOT every module
/// reached <see cref="ModuleProcessingStatus.Verified"/> (fixed Paso 5,
/// 2026-07-14 — see HUMAN-OS-STUDIO.md §14). Ends the run here instead of
/// proceeding to Experiencia/Gate 2/Publish — "all modules generated" is
/// NOT the same as "capability ready to assemble" (see
/// <see cref="ModuleCompletionGate"/>).
/// </summary>
internal sealed class ModuleRevisionRequiredExecutor : Executor<ModuleRouterOutput, ModuleGenerationOutcome>
{
    public ModuleRevisionRequiredExecutor() : base("ModuleRevisionRequired")
    {
    }

    public override async ValueTask<ModuleGenerationOutcome> HandleAsync(
        ModuleRouterOutput input,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var completed = input.Completed
            ?? throw new ArgumentException(
                "ModuleRevisionRequiredExecutor only handles routed messages with Completed set.");

        var outcome = new ModuleGenerationOutcome { BlueprintId = completed.BlueprintId, Modules = completed.Modules };

        await context.YieldOutputAsync(outcome, cancellationToken);
        return outcome;
    }
}
