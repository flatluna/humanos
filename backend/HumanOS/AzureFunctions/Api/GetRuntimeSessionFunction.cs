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
/// Rehydrates a Runtime session's current turn WITHOUT advancing it (Paso
/// 9, 2026-07-15) — resumes from the SQL-persisted checkpoint, drains
/// until the still-pending request re-surfaces (confirmed Paso 3 behavior:
/// a resumed run re-yields its pending <c>RequestInfoEvent</c>), and
/// returns it as-is. Lets a client reload/reconnect (e.g. after a page
/// refresh or app restart) without losing its place.
/// </summary>
public sealed class GetRuntimeSessionFunction
{
    private readonly TutorAgent _tutorAgent;
    private readonly CheckpointManager _checkpointManager;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public GetRuntimeSessionFunction(
        TutorAgent tutorAgent,
        CheckpointManager checkpointManager,
        IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _tutorAgent = tutorAgent;
        _checkpointManager = checkpointManager;
        _dbContextFactory = dbContextFactory;
    }

    [Function("GetRuntimeSession")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "sessions/{runtimeSessionId:guid}")]
        HttpRequestData request,
        Guid runtimeSessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var engineSessionId = runtimeSessionId.ToString("N");

            // MUST check this BEFORE ever resuming — resuming a run from a
            // checkpoint captured AT the terminal output does not
            // re-surface it and hangs forever (see RuntimeSessionStatus's
            // doc comment, found via live testing 2026-07-15).
            var terminalStatus = await RuntimeApiEngine.GetTerminalStatusAsync(
                _dbContextFactory, engineSessionId, cancellationToken);

            if (terminalStatus is { IsTerminal: true })
            {
                return await FunctionResponseFactory.SuccessResponseAsync(
                    request, RuntimeTurnResponse.FromTerminalStatus(runtimeSessionId, terminalStatus),
                    cancellationToken: cancellationToken);
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

            var drain = await RuntimeApiEngine.DrainAsync(run, cancellationToken);

            if (drain.Output is { } finalState)
            {
                await RuntimeApiEngine.MarkTerminalAsync(
                    _dbContextFactory, engineSessionId, finalState.Session.Stage.ToString(), cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, RuntimeTurnResponse.From(drain, runtimeSessionId), cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "GetRuntimeSessionFailed", ex.Message, cancellationToken);
        }
    }
}
