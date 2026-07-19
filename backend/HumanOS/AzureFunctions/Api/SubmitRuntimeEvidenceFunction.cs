using System.Net;
using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Runtime;
using HumanOS.Data;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Submits the learner's evidence for whatever Runtime stage is CURRENTLY
/// pending (Recall/Prediction/LearnerProduction/Reflection) and advances
/// the session to its next pause or terminal outcome (Paso 9, 2026-07-15).
/// Resumes purely from the SQL-persisted Workflow checkpoint (Paso 3) —
/// no other session registry needed.
/// </summary>
public sealed class SubmitRuntimeEvidenceFunction
{
    private readonly TutorAgent _tutorAgent;
    private readonly CheckpointManager _checkpointManager;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public SubmitRuntimeEvidenceFunction(
        TutorAgent tutorAgent,
        CheckpointManager checkpointManager,
        IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _tutorAgent = tutorAgent;
        _checkpointManager = checkpointManager;
        _dbContextFactory = dbContextFactory;
    }

    [Function("SubmitRuntimeEvidence")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "sessions/{runtimeSessionId:guid}/evidence")]
        HttpRequestData request,
        Guid runtimeSessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var engineSessionId = runtimeSessionId.ToString("N");

            // MUST check this BEFORE ever resuming — resuming a run from a
            // checkpoint captured AT the terminal output hangs forever
            // (see RuntimeSessionStatus's doc comment, found via live
            // testing 2026-07-15).
            var terminalStatus = await RuntimeApiEngine.GetTerminalStatusAsync(
                _dbContextFactory, engineSessionId, cancellationToken);

            if (terminalStatus is { IsTerminal: true })
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.Conflict, "RuntimeSessionAlreadyTerminal",
                    "This Runtime session has already finished — no more evidence can be submitted.",
                    cancellationToken);
            }

            var checkpoint = await RuntimeApiEngine.GetLatestCheckpointAsync(
                _dbContextFactory, engineSessionId, cancellationToken);

            if (checkpoint is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "RuntimeSessionNotFound",
                    $"No Runtime session found with id '{runtimeSessionId}'.", cancellationToken);
            }

            var workflow = RuntimeSessionWorkflowFactory.Build(_tutorAgent);
            await using var run = await InProcessExecution.ResumeStreamingAsync(
                workflow, checkpoint, _checkpointManager, cancellationToken);

            var beforeResponse = await RuntimeApiEngine.DrainAsync(run, cancellationToken);

            if (beforeResponse.Output is not null)
            {
                await RuntimeApiEngine.MarkTerminalAsync(
                    _dbContextFactory, engineSessionId, beforeResponse.Output.Session.Stage.ToString(), cancellationToken);

                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.Conflict, "RuntimeSessionAlreadyTerminal",
                    "This Runtime session has already finished — no more evidence can be submitted.",
                    cancellationToken);
            }

            if (beforeResponse.EvidenceRequest is not { } pendingEvidenceRequest)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.Conflict, "NotAwaitingEvidence",
                    "This Runtime session is currently presenting content (Instruction, a chapter's " +
                    "teaching content, or a chapter's mini-practice) — call the matching *-ack endpoint " +
                    "instead of submitting evidence (instruction-ack, chapter-ack, or " +
                    "chapter-mini-practice-ack).",
                    cancellationToken);
            }

            var body = await request.ReadFromJsonAsync<SubmitRuntimeEvidenceRequest>(cancellationToken)
                ?? new SubmitRuntimeEvidenceRequest();

            var evidence = RuntimeApiEngine.BuildEvidence(
                pendingEvidenceRequest,
                [.. body.Parts.Select(p => new StudentEvidencePart
                {
                    Kind = p.Kind,
                    Text = p.Text,
                    StorageUrl = p.StorageUrl,
                    MimeType = p.MimeType
                })],
                body.AssistanceLevel,
                body.CapturedBeforeAssistance,
                body.ComparesToEvidenceId);

            await run.SendResponseAsync(beforeResponse.PendingRequest!.CreateResponse(
                new EvidenceSubmission
                {
                    RuntimeSessionId = runtimeSessionId,
                    Evidence = evidence,
                    ForceAdvance = body.ForceAdvance
                }));

            var afterResponse = await RuntimeApiEngine.DrainAsync(run, cancellationToken);

            if (afterResponse.Output is { } finalState)
            {
                await RuntimeApiEngine.MarkTerminalAsync(
                    _dbContextFactory, engineSessionId, finalState.Session.Stage.ToString(), cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, RuntimeTurnResponse.From(afterResponse, runtimeSessionId), cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "SubmitRuntimeEvidenceFailed", ex.Message, cancellationToken);
        }
    }
}
