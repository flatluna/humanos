using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Interprets the human reviewer's GATE 2 decision (see
/// /memories/repo/humanstudio-multiagent-vision.md — "antes de PUBLICAR el
/// paquete final"). Returns a <see cref="Gate2Outcome"/>; downstream
/// conditional edges route the approved package to
/// <see cref="PublishExecutor"/>, or a rejection message to
/// <see cref="Gate2RejectionExecutor"/>.
/// </summary>
internal sealed class Gate2DecisionExecutor : Executor<GateDecision, Gate2Outcome>
{
    public Gate2DecisionExecutor() : base("Gate2Decision")
    {
    }

    public override async ValueTask<Gate2Outcome> HandleAsync(
        GateDecision decision,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var package = await context.ReadStateAsync<CapabilityPackage>(
            decision.SubjectId.ToString(),
            scopeName: ExperienciaExecutor.PackageStateScope);

        if (package is null)
        {
            return new Gate2Outcome
            {
                RejectionMessage = $"Gate 2: no pending package found for id '{decision.SubjectId}'."
            };
        }

        if (!decision.Approved)
        {
            return new Gate2Outcome
            {
                RejectionMessage = $"Gate 2 REJECTED package '{package.CapabilityName}': {decision.Comments}"
            };
        }

        return new Gate2Outcome { ApprovedPackage = package };
    }
}
