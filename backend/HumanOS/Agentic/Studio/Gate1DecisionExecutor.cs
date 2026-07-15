using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Interprets the human reviewer's GATE 1 decision (see
/// /memories/repo/humanstudio-multiagent-vision.md — "after Arquitecto,
/// before content generation"). Returns a <see cref="Gate1Outcome"/>;
/// downstream conditional edges route the approved blueprint to the
/// module pipeline, or a rejection message to
/// <see cref="Gate1RejectionExecutor"/>.
/// </summary>
internal sealed class Gate1DecisionExecutor : Executor<GateDecision, Gate1Outcome>
{
    public Gate1DecisionExecutor() : base("Gate1Decision")
    {
    }

    public override async ValueTask<Gate1Outcome> HandleAsync(
        GateDecision decision,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await context.ReadStateAsync<PipelineState>(
            decision.SubjectId.ToString(),
            scopeName: ArquitectoExecutor.PipelineStateScope);

        if (state is null)
        {
            return new Gate1Outcome
            {
                RejectionMessage = $"Gate 1: no pending blueprint found for id '{decision.SubjectId}'."
            };
        }

        if (!decision.Approved)
        {
            return new Gate1Outcome
            {
                RejectionMessage =
                    $"Gate 1 REJECTED blueprint '{state.Blueprint.CapabilityName}': {decision.Comments}"
            };
        }

        var blueprintToUse = state.Blueprint;
        if (decision.RevisedBlueprint is not null)
        {
            // Trust the URL-derived SubjectId, not any client-supplied id — BlueprintId
            // is [JsonIgnore]d (see ArquitectoAgent.cs) so it always deserializes as a
            // fresh GUID anyway. Keep the original curated corpus: Instructor and
            // Experiencia still need it even though the blueprint was trimmed/edited.
            blueprintToUse = decision.RevisedBlueprint;
            blueprintToUse.BlueprintId = decision.SubjectId;

            await context.QueueStateUpdateAsync(
                decision.SubjectId.ToString(),
                new PipelineState
                {
                    CapabilityDomainId = state.CapabilityDomainId,
                    Blueprint = blueprintToUse,
                    CuratedCorpus = state.CuratedCorpus
                },
                scopeName: ArquitectoExecutor.PipelineStateScope);
        }

        return new Gate1Outcome { ApprovedBlueprint = blueprintToUse };
    }
}

