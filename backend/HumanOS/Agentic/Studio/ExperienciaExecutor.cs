using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Workflow executor wrapping <see cref="ExperienciaAgent"/>. Accepts the
/// shared <see cref="ModuleRouterOutput"/> type (only the Completed
/// branch is valid here). Its output (a <see cref="CapabilityPackage"/>)
/// is sent to the Gate 2 <see cref="RequestPort{TRequest,TResponse}"/> for
/// human review before publishing.
/// </summary>
internal sealed class ExperienciaExecutor : Executor<ModuleRouterOutput, CapabilityPackage>
{
    /// <summary>Shared-state scope name for the assembled package, keyed
    /// by PackageId, read by Gate2DecisionExecutor.</summary>
    internal const string PackageStateScope = "StudioPackageState";

    private readonly ExperienciaAgent _agent;

    public ExperienciaExecutor(ExperienciaAgent agent) : base("Experiencia")
    {
        _agent = agent;
    }

    public override async ValueTask<CapabilityPackage> HandleAsync(
        ModuleRouterOutput input,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var completed = input.Completed
            ?? throw new ArgumentException("ExperienciaExecutor only handles routed messages with Completed set.");

        var state = await context.ReadStateAsync<PipelineState>(
            completed.BlueprintId.ToString(),
            scopeName: ArquitectoExecutor.PipelineStateScope);

        if (state is null)
        {
            throw new InvalidOperationException(
                $"No pipeline state found for blueprint '{completed.BlueprintId}'.");
        }

        var package = await _agent.AssembleAsync(state.Blueprint, completed.Modules, cancellationToken);

        // Paso 3 (2026-07-14): merge the run's accumulated token usage
        // (Curador + Arquitecto + one entry per Instructor/Métrico call)
        // with the Experiencia call that just happened, so the final
        // CapabilityPackage carries the FULL per-agent-call breakdown for
        // the whole run (observability only).
        var allTokenUsage = new List<AgentTokenUsage>(state.TokenUsage);
        allTokenUsage.AddRange(package.TokenUsage);
        package.TokenUsage = allTokenUsage;

        await context.QueueStateUpdateAsync(
            package.PackageId.ToString(),
            package,
            scopeName: PackageStateScope);

        return package;
    }
}
