using System.Net;
using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// TEMPORARY smoke test (2026-07-14) — proves Paso 4's minimal deliverable:
/// Runtime -&gt; TutorTurnContext -&gt; Tutor Agent -&gt; Response, with the
/// Runtime (not the Tutor) deciding stage/contract/permissions. Same
/// "build, verify, delete" pattern as
/// <see cref="RuntimeCheckpointSmokeTestFunction"/>. No Assessment, no
/// progression, no tools yet — deliberately out of scope for this step.
/// </summary>
public sealed class TutorAgentSmokeTestFunction
{
    private readonly TutorAgent _tutorAgent;

    public TutorAgentSmokeTestFunction(TutorAgent tutorAgent)
    {
        _tutorAgent = tutorAgent;
    }

    [Function("TutorAgentSmokeTest")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/runtime/tutor-smoke-test")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_tutorAgent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "TutorAgentNotConfigured",
                "TutorAgent is not configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        try
        {
            // The Runtime builds the session and decides the stage — the
            // Tutor never invents any of this.
            var session = new RuntimeSession
            {
                RuntimeSessionId = Guid.NewGuid(),
                PersonId = Guid.NewGuid(),
                CapabilityModuleId = Guid.NewGuid(),
                Stage = RuntimeStage.RecallRequired,
                Contract = new RuntimePedagogicalContract
                {
                    CapabilityModuleId = Guid.NewGuid(),
                    TargetMetric = CapabilityMetric.Recall,
                    RecallRequirement = "Recuerda sin ayuda los 3 componentes de una agenda accionable.",
                    LearnerProduction = "Una lista escrita de los 3 componentes, de memoria.",
                    SuccessCriteria = ["Menciona propósito, resultado esperado y responsables"]
                }
            };

            var state = new RuntimeSessionState { Session = session };

            // The Runtime — via TutorTurnContextBuilder, NOT the Tutor —
            // decides the permissions for this turn (Recall must block
            // knowledge access).
            var context = TutorTurnContextBuilder.Build(state, RuntimeStage.RecallRequired);

            var result = await _tutorAgent.RespondAsync(context, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(request, new
            {
                Success = true,
                Stage = context.CurrentStage.ToString(),
                KnowledgeAccessAllowed = context.Permissions.KnowledgeAccessAllowed,
                AllowedTools = context.Permissions.AllowedTools,
                TutorResponse = result.Response
            }, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "TutorAgentSmokeTestFailed", ex.ToString(), cancellationToken);
        }
    }
}
