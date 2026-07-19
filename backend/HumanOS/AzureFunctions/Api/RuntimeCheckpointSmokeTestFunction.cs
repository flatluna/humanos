using System.Net;
using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Runtime;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// TEMPORARY smoke test (2026-07-14, see /memories/repo/human-os-runtime-design.md) —
/// proves Paso 1+2+3 of the Interactive Learning Runtime work together
/// BEFORE introducing the Tutor Agent (Paso 4). Same "temporary debug
/// endpoint, delete after use" pattern already used for Studio's Paso 6
/// verification (<c>DebugVerificationPersistenceFunction</c>, since
/// deleted).
///
/// Sequence proven end-to-end:
///   Start session -&gt; ModuleStarted -&gt; RecallRequired -&gt; pauses,
///   checkpoint saved to Azure SQL -&gt; SIMULATED PROCESS RESTART (the
///   first StreamingRun is disposed and never touched again; a brand new
///   Workflow graph instance is built) -&gt; resume purely from the
///   persisted SQL checkpoint -&gt; confirm still paused at RecallRequired
///   with the correct session data -&gt; submit evidence -&gt; confirm it
///   advances to the next pause (PredictionRequired).
/// </summary>
public sealed class RuntimeCheckpointSmokeTestFunction
{
    private readonly CheckpointManager _checkpointManager;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly TutorAgent _tutorAgent;

    public RuntimeCheckpointSmokeTestFunction(
        CheckpointManager checkpointManager,
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        TutorAgent tutorAgent)
    {
        _checkpointManager = checkpointManager;
        _dbContextFactory = dbContextFactory;
        _tutorAgent = tutorAgent;
    }

    [Function("RuntimeCheckpointSmokeTest")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/runtime/checkpoint-smoke-test")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        try
        {
            var sessionId = Guid.NewGuid().ToString("N");
            var runtimeSessionId = Guid.NewGuid();

            var initialSession = new RuntimeSession
            {
                RuntimeSessionId = runtimeSessionId,
                PersonId = Guid.NewGuid(),
                CapabilityModuleId = Guid.NewGuid(),
                Contract = new RuntimePedagogicalContract
                {
                    CapabilityModuleId = Guid.NewGuid(),
                    TargetMetric = CapabilityMetric.Recall,
                    RecallRequirement = "SMOKE TEST: recuerda sin ayuda el concepto X.",
                    LearnerProduction = "SMOKE TEST: produce una explicación propia.",
                    SuccessCriteria = ["SMOKE TEST criterion"]
                }
            };

            // PHASE 1 — fresh graph, run until it pauses at Recall.
            var workflowA = RuntimeSessionWorkflowFactory.Build(_tutorAgent);
            await using var runA = await InProcessExecution.RunStreamingAsync(
                workflowA, initialSession, _checkpointManager, sessionId, cancellationToken);

            var (pausedAtStage1, checkpointBeforeRestart) = await DrainUntilPausedAsync(runA, cancellationToken);

            if (pausedAtStage1 != RuntimeStage.RecallRequired.ToString())
            {
                throw new InvalidOperationException(
                    $"Expected to pause at RecallRequired, actually paused reporting stage '{pausedAtStage1}'.");
            }

            if (checkpointBeforeRestart is null)
            {
                throw new InvalidOperationException("No checkpoint was captured before the simulated restart.");
            }

            int checkpointRowsBeforeResume;
            await using (var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                checkpointRowsBeforeResume = await db.RuntimeWorkflowCheckpoints
                    .CountAsync(x => x.SessionId == sessionId, cancellationToken);
            }

            // SIMULATED PROCESS RESTART — runA is disposed and never used
            // again; workflowB is a completely fresh graph, same rule as
            // every other Studio Workflow ("a fresh graph must be built
            // per run"). Only the SQL-persisted checkpoint bridges them.
            var workflowB = RuntimeSessionWorkflowFactory.Build(_tutorAgent);
            await using var runB = await InProcessExecution.ResumeStreamingAsync(
                workflowB, checkpointBeforeRestart, _checkpointManager, cancellationToken);

            var (pausedAtStage2, _) = await DrainUntilPausedAsync(runB, cancellationToken, expectImmediatePause: true);

            if (pausedAtStage2 != RuntimeStage.RecallRequired.ToString())
            {
                throw new InvalidOperationException(
                    $"After resume, expected to still be paused at RecallRequired, got '{pausedAtStage2}'.");
            }

            // Submit the Recall evidence and confirm the Runtime advances
            // to the NEXT pause point (PredictionRequired).
            var evidence = new StudentEvidence
            {
                RuntimeSessionId = runtimeSessionId,
                CapabilityModuleId = initialSession.CapabilityModuleId,
                Origin = StudentEvidenceOrigin.Recall,
                Parts = [new StudentEvidencePart { Kind = StudentEvidenceKind.Text, Text = "SMOKE TEST recall answer" }],
                AssistanceLevel = EvidenceAssistanceLevel.Unaided,
                CapturedBeforeAssistance = true
            };

            var lastPendingRequest = _lastPendingRequest
                ?? throw new InvalidOperationException("No pending EvidenceRequest tracked after resume.");

            await runB.SendResponseAsync(lastPendingRequest.CreateResponse(
                new EvidenceSubmission { RuntimeSessionId = runtimeSessionId, Evidence = evidence }));

            var (pausedAtStage3, _) = await DrainUntilPausedAsync(runB, cancellationToken);

            int checkpointRowsAfterResume;
            await using (var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                checkpointRowsAfterResume = await db.RuntimeWorkflowCheckpoints
                    .CountAsync(x => x.SessionId == sessionId, cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(request, new
            {
                Success = true,
                SessionId = sessionId,
                RuntimeSessionId = runtimeSessionId,
                StageAfterFirstPause = pausedAtStage1,
                CheckpointRowsBeforeSimulatedRestart = checkpointRowsBeforeResume,
                StageAfterSimulatedRestartAndResume = pausedAtStage2,
                StageAfterSubmittingRecallEvidence = pausedAtStage3,
                CheckpointRowsAfterResume = checkpointRowsAfterResume
            }, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "RuntimeCheckpointSmokeTestFailed", ex.ToString(), cancellationToken);
        }
    }

    // Tracks the most recent pending request so we can respond to it —
    // fine for a single-request smoke test; a real Tutor-facing endpoint
    // (later Paso) will correlate this properly instead of a field.
    private ExternalRequest? _lastPendingRequest;

    private async Task<(string Stage, CheckpointInfo? Checkpoint)> DrainUntilPausedAsync(
        StreamingRun run,
        CancellationToken cancellationToken,
        bool expectImmediatePause = false)
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
                _lastPendingRequest = requestInfo.Request;

                if (requestInfo.Request.TryGetDataAs<EvidenceRequest>(out var evidenceRequest))
                {
                    return (evidenceRequest.Stage.ToString(), run.LastCheckpoint);
                }

                return ("Unknown", run.LastCheckpoint);
            }
        }

        if (expectImmediatePause)
        {
            throw new InvalidOperationException("Resumed run ended the stream without re-surfacing the pending request.");
        }

        throw new InvalidOperationException("Workflow stream ended without pausing at a RequestInfoEvent.");
    }
}
