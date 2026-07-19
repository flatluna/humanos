using System.Net;
using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// TEMPORARY smoke test (2026-07-14) — proves Paso 8's Progression logic
/// end-to-end through the REAL Workflow graph (not a hand-built
/// TutorTurnContext like the Paso 6 smoke test): a full module run that
/// deliberately submits a BAD LearnerProduction first (to exercise the
/// retry loop), then a GOOD one, then completes through Reflection. Same
/// "build, verify, delete" pattern as the other smoke tests.
/// </summary>
public sealed class ProgressionSmokeTestFunction
{
    private readonly TutorAgent _tutorAgent;

    public ProgressionSmokeTestFunction(TutorAgent tutorAgent)
    {
        _tutorAgent = tutorAgent;
    }

    [Function("ProgressionSmokeTest")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/runtime/progression-smoke-test")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_tutorAgent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "TutorAgentNotConfigured",
                "TutorAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        var log = new List<string>();

        try
        {
            var runtimeSessionId = Guid.NewGuid();
            var session = new RuntimeSession
            {
                RuntimeSessionId = runtimeSessionId,
                PersonId = Guid.NewGuid(),
                CapabilityModuleId = Guid.NewGuid(),
                Contract = new RuntimePedagogicalContract
                {
                    CapabilityModuleId = Guid.NewGuid(),
                    TargetMetric = CapabilityMetric.Application,
                    RecallRequirement = "Recuerda de memoria los 3 componentes de una agenda accionable.",
                    LearnerProduction = "Una agenda real con propósito, tiempos y responsables.",
                    SuccessCriteria =
                    [
                        "El propósito define una decisión o entrega concreta.",
                        "Cada elemento tiene objetivo, tiempo y responsable."
                    ]
                }
            };

            var workflow = RuntimeSessionWorkflowFactory.Build(_tutorAgent);
            await using var run = await InProcessExecution.RunStreamingAsync(
                workflow, session, cancellationToken: cancellationToken);

            ExternalRequest? pending = null;

            async Task<string> DrainAsync()
            {
                await foreach (var evt in run.WatchStreamAsync(cancellationToken))
                {
                    if (evt is ExecutorFailedEvent failed)
                    {
                        throw new InvalidOperationException($"Executor '{failed.ExecutorId}' failed: {failed.Data}");
                    }

                    if (evt is WorkflowErrorEvent error)
                    {
                        throw new InvalidOperationException($"Workflow error: {error.Exception}");
                    }

                    if (evt is RequestInfoEvent requestInfo)
                    {
                        pending = requestInfo.Request;

                        if (requestInfo.Request.TryGetDataAs<EvidenceRequest>(out var evidenceRequest) &&
                            !string.IsNullOrEmpty(evidenceRequest.Prompt))
                        {
                            log.Add($"PAUSED at {evidenceRequest.Stage}: {evidenceRequest.Prompt}");
                            return evidenceRequest.Stage.ToString();
                        }

                        if (requestInfo.Request.TryGetDataAs<InstructionPresentation>(out var instructionPresentation) &&
                            !string.IsNullOrEmpty(instructionPresentation.Content))
                        {
                            log.Add($"PAUSED at Instruction: {instructionPresentation.Content}");
                            return RuntimeStage.Instruction.ToString();
                        }

                        log.Add("PAUSED at Unknown request type");
                        return "Unknown";
                    }

                    if (evt is WorkflowOutputEvent output && output.Data is RuntimeSessionState finalState)
                    {
                        log.Add($"COMPLETED at stage {finalState.Session.Stage}");
                        return finalState.Session.Stage.ToString();
                    }
                }

                throw new InvalidOperationException("Workflow stream ended unexpectedly.");
            }

            async Task RespondAsync(StudentEvidenceOrigin origin, string text)
            {
                var req = pending ?? throw new InvalidOperationException("No pending request to respond to.");
                var evidence = new StudentEvidence
                {
                    RuntimeSessionId = runtimeSessionId,
                    CapabilityModuleId = session.CapabilityModuleId,
                    Origin = origin,
                    Parts = [new StudentEvidencePart { Kind = StudentEvidenceKind.Text, Text = text }],
                    AssistanceLevel = EvidenceAssistanceLevel.Unaided,
                    CapturedBeforeAssistance = origin is StudentEvidenceOrigin.Recall or StudentEvidenceOrigin.Prediction
                };
                await run.SendResponseAsync(req.CreateResponse(
                    new EvidenceSubmission { RuntimeSessionId = runtimeSessionId, Evidence = evidence }));
            }

            async Task AcknowledgeInstructionAsync()
            {
                var req = pending ?? throw new InvalidOperationException("No pending request to respond to.");
                await run.SendResponseAsync(req.CreateResponse(
                    new InstructionAcknowledgement { RuntimeSessionId = runtimeSessionId }));
            }

            var stage1 = await DrainAsync(); // RecallRequired
            await RespondAsync(StudentEvidenceOrigin.Recall, "Propósito, agenda con tiempos, responsables asignados.");

            var stage2 = await DrainAsync(); // PredictionRequired
            await RespondAsync(StudentEvidenceOrigin.Prediction, "Predigo que necesitaré especificar tiempos por punto.");

            var stageInstruction = await DrainAsync(); // Instruction (real Tutor-phrased content, now pauses)
            await AcknowledgeInstructionAsync();

            var stage3 = await DrainAsync(); // LearnerProduction (attempt 1 — BAD on purpose)
            await RespondAsync(StudentEvidenceOrigin.Production, "Reunión para hablar de cosas del equipo.");

            var stage4 = await DrainAsync(); // Expected: LearnerProduction again (retry)
            await RespondAsync(StudentEvidenceOrigin.Production,
                "Agenda reunión Q3: Propósito: decidir presupuesto de marketing Q3. " +
                "1. Revisión Q2 (15 min) - Ana. 2. Propuesta Q3 (20 min) - Luis. 3. Decisión final (10 min) - Ana.");

            var stage5 = await DrainAsync(); // Expected: Reflection (this time Verified)
            await RespondAsync(StudentEvidenceOrigin.Reflection,
                "Predije que necesitaría tiempos por punto — así fue, y también necesité un responsable por punto.");

            var finalStage = await DrainAsync(); // Expected: Completed

            return await FunctionResponseFactory.SuccessResponseAsync(request, new
            {
                Success = true,
                Stage1_ExpectedRecallRequired = stage1,
                Stage2_ExpectedPredictionRequired = stage2,
                StageInstruction_ExpectedInstruction = stageInstruction,
                Stage3_ExpectedLearnerProduction_FirstAttempt = stage3,
                Stage4_ExpectedLearnerProduction_RetryAfterBadSubmission = stage4,
                Stage5_ExpectedReflection_AfterGoodSubmission = stage5,
                FinalStage_ExpectedCompleted = finalStage,
                Log = log
            }, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "ProgressionSmokeTestFailed",
                ex.ToString() + "\n\nLOG:\n" + string.Join("\n", log), cancellationToken);
        }
    }
}
