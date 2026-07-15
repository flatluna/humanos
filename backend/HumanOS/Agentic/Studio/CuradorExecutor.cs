using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Workflow executor wrapping <see cref="CuradorAgent"/> — the first step
/// of the Human OS Studio capability-creation pipeline (see
/// /memories/repo/humanstudio-multiagent-vision.md). Entry point of the
/// workflow graph.
/// </summary>
internal sealed class CuradorExecutor : Executor<RawMaterialBatch, CuratorOutput>
{
    private readonly CuradorAgent _agent;

    public CuradorExecutor(CuradorAgent agent) : base("Curador")
    {
        _agent = agent;
    }

    public override async ValueTask<CuratorOutput> HandleAsync(
        RawMaterialBatch batch,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await _agent.CurateAsync(batch.Materials, cancellationToken);

        return new CuratorOutput
        {
            CapabilityDomainId = batch.CapabilityDomainId,
            CapabilityGoal = batch.CapabilityGoal,
            Corpus = result.Corpus,
            TokenUsage = result.TokenUsage
        };
    }
}
