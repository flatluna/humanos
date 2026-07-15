using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Workflow executor wrapping <see cref="ArquitectoAgent"/>. Its output
/// (a <see cref="CapabilityBlueprint"/>) is sent to the Gate 1
/// <see cref="RequestPort{TRequest,TResponse}"/> for human review before
/// any module content is generated.
/// </summary>
internal sealed class ArquitectoExecutor : Executor<CuratorOutput, CapabilityBlueprint>
{
    /// <summary>Shared-state scope name for blueprint + curated-corpus
    /// pairs, keyed by BlueprintId, read by Gate1DecisionExecutor,
    /// InstructorExecutor and ExperienciaExecutor.</summary>
    internal const string PipelineStateScope = "StudioPipelineState";

    private readonly ArquitectoAgent _agent;

    public ArquitectoExecutor(ArquitectoAgent agent) : base("Arquitecto")
    {
        _agent = agent;
    }

    public override async ValueTask<CapabilityBlueprint> HandleAsync(
        CuratorOutput input,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await _agent.DesignAsync(input.CapabilityGoal, input.Corpus, cancellationToken);
        var blueprint = result.Blueprint;

        await context.QueueStateUpdateAsync(
            blueprint.BlueprintId.ToString(),
            new PipelineState
            {
                CapabilityDomainId = input.CapabilityDomainId,
                Blueprint = blueprint,
                CuratedCorpus = input.Corpus,
                // Paso 3 (2026-07-14): seed the per-run token usage log with
                // the Curador + Arquitecto calls that already happened —
                // Instructor/Métrico/Experiencia append to this same list
                // as the run progresses (observability only).
                TokenUsage = [input.TokenUsage, result.TokenUsage]
            },
            scopeName: PipelineStateScope);

        return blueprint;
    }
}
