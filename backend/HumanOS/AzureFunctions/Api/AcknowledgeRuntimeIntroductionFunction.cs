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
/// Acknowledges the module's introduction (presented once at
/// ModuleStarted, before any Recall attempt — fixed 2026-07-16, see
/// <see cref="RuntimeSessionWorkflowFactory"/>'s doc comment) and advances
/// the session to RecallRequired. The introduction never produces
/// <see cref="StudentEvidence"/> (same rationale as
/// <see cref="AcknowledgeRuntimeInstructionFunction"/>) — this endpoint
/// carries no evidence body, only a simple "I've read this, continue".
/// </summary>
public sealed class AcknowledgeRuntimeIntroductionFunction
{
    private readonly TutorAgent _tutorAgent;
    private readonly CheckpointManager _checkpointManager;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public AcknowledgeRuntimeIntroductionFunction(
        TutorAgent tutorAgent,
        CheckpointManager checkpointManager,
        IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _tutorAgent = tutorAgent;
        _checkpointManager = checkpointManager;
        _dbContextFactory = dbContextFactory;
    }

    [Function("AcknowledgeRuntimeIntroduction")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "sessions/{runtimeSessionId:guid}/introduction-ack")]
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

            if (beforeResponse.IntroductionPresentation is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.Conflict, "NotAwaitingIntroductionAcknowledgement",
                    "This Runtime session is not currently presenting the module introduction — " +
                    "submit evidence instead.", cancellationToken);
            }

            await run.SendResponseAsync(beforeResponse.PendingRequest!.CreateResponse(
                new IntroductionAcknowledgement { RuntimeSessionId = runtimeSessionId }));

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
                request, HttpStatusCode.InternalServerError, "AcknowledgeRuntimeIntroductionFailed", ex.Message, cancellationToken);
        }
    }
}
