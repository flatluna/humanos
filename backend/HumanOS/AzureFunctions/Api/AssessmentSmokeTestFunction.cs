using System.Net;
using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// TEMPORARY smoke test (2026-07-14) — proves Paso 6's Assessment
/// capability: Tutor Agent verdict + deterministic
/// <c>RuntimeAssessmentValidator</c>, against synthetic but realistic
/// learner evidence (one clearly-satisfying case, one clearly-failing
/// case). Same "build, verify, delete" pattern as the Paso 3/4 smoke
/// tests. No workflow/executor wiring yet — that is Paso 8 (Progression).
/// </summary>
public sealed class AssessmentSmokeTestFunction
{
    private readonly TutorAgent _tutorAgent;

    public AssessmentSmokeTestFunction(TutorAgent tutorAgent)
    {
        _tutorAgent = tutorAgent;
    }

    [Function("AssessmentSmokeTest")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/runtime/assessment-smoke-test")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_tutorAgent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "TutorAgentNotConfigured",
                "TutorAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        try
        {
            var contract = new RuntimePedagogicalContract
            {
                CapabilityModuleId = Guid.NewGuid(),
                TargetMetric = CapabilityMetric.Application,
                RecallRequirement = "Recuerda de memoria los componentes de una agenda accionable.",
                LearnerProduction = "Una agenda para una reunión real con propósito, resultado y responsables.",
                SuccessCriteria =
                [
                    "El propósito define una decisión o entrega concreta.",
                    "Cada elemento tiene objetivo, tiempo y responsable."
                ]
            };

            // A GOOD submission — should end up Verified.
            var goodContext = new TutorTurnContext
            {
                RuntimeSessionId = Guid.NewGuid(),
                CurrentStage = RuntimeStage.Assessment,
                Contract = contract,
                AccumulatedEvidence =
                [
                    new StudentEvidence
                    {
                        RuntimeSessionId = Guid.NewGuid(),
                        CapabilityModuleId = contract.CapabilityModuleId,
                        Origin = StudentEvidenceOrigin.Production,
                        Parts =
                        [
                            new StudentEvidencePart
                            {
                                Kind = StudentEvidenceKind.Text,
                                Text = "Agenda reunión de planificación Q3:\n" +
                                       "Propósito: decidir el presupuesto de marketing para Q3.\n" +
                                       "1. Revisión de resultados Q2 (15 min) - Responsable: Ana\n" +
                                       "2. Propuesta de presupuesto Q3 (20 min) - Responsable: Luis\n" +
                                       "3. Decisión final y próximos pasos (10 min) - Responsable: Ana"
                            }
                        ],
                        AssistanceLevel = EvidenceAssistanceLevel.Unaided,
                        CapturedBeforeAssistance = false
                    }
                ],
                Permissions = new TutorToolPermissions { KnowledgeAccessAllowed = true }
            };

            var goodResult = await _tutorAgent.AssessAsync(goodContext, cancellationToken);

            // A BAD submission — should end up NotVerified or Failed (no
            // real purpose/timing/responsible present at all).
            var badContext = new TutorTurnContext
            {
                RuntimeSessionId = Guid.NewGuid(),
                CurrentStage = RuntimeStage.Assessment,
                Contract = contract,
                AccumulatedEvidence =
                [
                    new StudentEvidence
                    {
                        RuntimeSessionId = Guid.NewGuid(),
                        CapabilityModuleId = contract.CapabilityModuleId,
                        Origin = StudentEvidenceOrigin.Production,
                        Parts =
                        [
                            new StudentEvidencePart
                            {
                                Kind = StudentEvidenceKind.Text,
                                Text = "Reunión para hablar de cosas del equipo."
                            }
                        ],
                        AssistanceLevel = EvidenceAssistanceLevel.Unaided,
                        CapturedBeforeAssistance = false
                    }
                ],
                Permissions = new TutorToolPermissions { KnowledgeAccessAllowed = true }
            };

            var badResult = await _tutorAgent.AssessAsync(badContext, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(request, new
            {
                Success = true,
                GoodSubmission = new
                {
                    Status = goodResult.Status.ToString(),
                    goodResult.Explanation,
                    Criteria = goodResult.SuccessCriteriaResults
                },
                BadSubmission = new
                {
                    Status = badResult.Status.ToString(),
                    badResult.Explanation,
                    Criteria = badResult.SuccessCriteriaResults
                }
            }, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "AssessmentSmokeTestFailed", ex.ToString(), cancellationToken);
        }
    }
}
