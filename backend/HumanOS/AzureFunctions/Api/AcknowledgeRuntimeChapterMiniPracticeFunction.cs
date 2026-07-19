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
/// Acknowledges the currently-presented chapter's <c>MiniPracticePrompt</c>
/// and advances the session to that chapter's Recall turn (fixed
/// 2026-07-16 — phase-based Chapters loop, see
/// <see cref="RuntimeStage.ChapterMiniPractice"/>'s doc comment). Mirrors
/// <see cref="AcknowledgeRuntimeChapterFunction"/> exactly — private
/// off-app retrieval practice, never graded, never produces
/// <see cref="StudentEvidence"/>.
/// </summary>
public sealed class AcknowledgeRuntimeChapterMiniPracticeFunction
{
    private readonly TutorAgent _tutorAgent;
    private readonly CheckpointManager _checkpointManager;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public AcknowledgeRuntimeChapterMiniPracticeFunction(
        TutorAgent tutorAgent,
        CheckpointManager checkpointManager,
        IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _tutorAgent = tutorAgent;
        _checkpointManager = checkpointManager;
        _dbContextFactory = dbContextFactory;
    }

    [Function("AcknowledgeRuntimeChapterMiniPractice")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "sessions/{runtimeSessionId:guid}/chapter-mini-practice-ack")]
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
                    "This Runtime session has already finished.", cancellationToken);
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
                    "This Runtime session has already finished.", cancellationToken);
            }

            if (beforeResponse.ChapterMiniPracticePresentation is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.Conflict, "NotAwaitingChapterMiniPracticeAcknowledgement",
                    "This Runtime session is not currently presenting a chapter's mini-practice — " +
                    "submit evidence instead.", cancellationToken);
            }

            await run.SendResponseAsync(beforeResponse.PendingRequest!.CreateResponse(
                new ChapterMiniPracticeAcknowledgement { RuntimeSessionId = runtimeSessionId }));

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
                request, HttpStatusCode.InternalServerError, "AcknowledgeRuntimeChapterMiniPracticeFailed", ex.Message, cancellationToken);
        }
    }
}
