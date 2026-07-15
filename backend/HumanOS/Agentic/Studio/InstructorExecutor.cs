using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>Workflow executor wrapping <see cref="InstructorAgent"/>.
/// Accepts the shared <see cref="ModuleRouterOutput"/> type (only the
/// NextModule branch is valid here) so it can be fed by either
/// ModuleQueueInitializerExecutor or ModuleCompletionRouterExecutor via a
/// conditional edge — same pattern as the Agent Framework's
/// conditional-edges sample.</summary>
internal sealed class InstructorExecutor : Executor<ModuleRouterOutput, ModuleScriptWorkItem>
{
    private readonly InstructorAgent _agent;

    public InstructorExecutor(InstructorAgent agent) : base("Instructor")
    {
        _agent = agent;
    }

    public override async ValueTask<ModuleScriptWorkItem> HandleAsync(
        ModuleRouterOutput input,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var item = input.NextModule
            ?? throw new ArgumentException("InstructorExecutor only handles routed messages with NextModule set.");

        // Progress event only (does not pause the workflow) — lets the
        // orchestrator's background drain loop report per-module progress
        // to the frontend's polling UI, without touching GATE 1/GATE 2.
        await context.AddEventAsync(
            new ModuleScriptStartedEvent(item.Module.ModuleId, item.Module.Title), cancellationToken);

        var state = await context.ReadStateAsync<PipelineState>(
            item.BlueprintId.ToString(),
            scopeName: ArquitectoExecutor.PipelineStateScope);

        var corpus = state?.CuratedCorpus ?? new CuratedCorpus();
        var result = await _agent.WriteScriptAsync(item.Layer, item.Module, corpus, item.Revision, cancellationToken);

        // Paso 3 (2026-07-14): record the Instructor's token usage for this
        // module into the shared per-run state — secondary/observability
        // concern only, does not affect the pedagogical result above.
        if (state is not null)
        {
            state.TokenUsage.Add(result.TokenUsage);
            await context.QueueStateUpdateAsync(
                item.BlueprintId.ToString(),
                state,
                scopeName: ArquitectoExecutor.PipelineStateScope);
        }

        return new ModuleScriptWorkItem { Item = item, Script = result.Script };
    }
}
