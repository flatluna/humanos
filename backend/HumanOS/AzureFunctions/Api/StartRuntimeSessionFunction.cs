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
/// Starts a new Interactive Learning Runtime session for one learner/module
/// (Paso 9, 2026-07-15, see /memories/repo/human-os-runtime-design.md) —
/// the first REAL (non-smoke-test) Runtime endpoint. Loads the
/// Studio-approved <c>CapabilityModule</c>, projects its
/// <see cref="RuntimePedagogicalContract"/>, and runs the Workflow
/// (Paso 2/8's real graph, Paso 9's real Tutor-phrased content) until its
/// first pause — always <see cref="RuntimeStage.ModuleStarted"/> (the
/// Tutor's real introduction, fixed 2026-07-16 — see
/// <see cref="RuntimeSessionWorkflowFactory"/>'s doc comment for why this
/// now comes before <see cref="RuntimeStage.RecallRequired"/> rather than
/// the session opening directly on an unassisted Recall attempt).
/// </summary>
public sealed class StartRuntimeSessionFunction
{
    private readonly TutorAgent _tutorAgent;
    private readonly CheckpointManager _checkpointManager;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public StartRuntimeSessionFunction(
        TutorAgent tutorAgent,
        CheckpointManager checkpointManager,
        IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _tutorAgent = tutorAgent;
        _checkpointManager = checkpointManager;
        _dbContextFactory = dbContextFactory;
    }

    [Function("StartRuntimeSession")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/modules/{capabilityModuleId:guid}/sessions")]
        HttpRequestData request,
        Guid personId,
        Guid capabilityModuleId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token
        // (same open item as StartAssessmentAttemptFunction).
        if (!_tutorAgent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "TutorAgentNotConfigured",
                "TutorAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var module = await db.CapabilityModules
                .Include(m => m.Verifications).ThenInclude(v => v.SuccessCriteriaResults)
                .Include(m => m.Chapters)
                .Include(m => m.CapabilityLevel).ThenInclude(l => l.Capability)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.CapabilityModuleId == capabilityModuleId, cancellationToken);

            if (module is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "CapabilityModuleNotFound",
                    $"No CapabilityModule found with id '{capabilityModuleId}'.", cancellationToken);
            }

            RuntimePedagogicalContract contract;
            try
            {
                contract = RuntimePedagogicalContractProjector.Project(module);
            }
            catch (InvalidOperationException ex)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.UnprocessableEntity, "ModuleNotReadyForRuntime", ex.Message, cancellationToken);
            }

            var session = new RuntimeSession
            {
                PersonId = personId,
                CapabilityModuleId = capabilityModuleId,
                Contract = contract
            };

            var engineSessionId = session.RuntimeSessionId.ToString("N");

            var workflow = RuntimeSessionWorkflowFactory.Build(_tutorAgent);
            await using var run = await InProcessExecution.RunStreamingAsync(
                workflow, session, _checkpointManager, engineSessionId, cancellationToken);

            var drain = await RuntimeApiEngine.DrainAsync(run, cancellationToken);

            if (drain.Output is { } finalState)
            {
                await RuntimeApiEngine.MarkTerminalAsync(
                    _dbContextFactory, engineSessionId, finalState.Session.Stage.ToString(), cancellationToken);
            }

            return await FunctionResponseFactory.CreatedResponseAsync(
                request, RuntimeTurnResponse.From(drain, session.RuntimeSessionId), cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "StartRuntimeSessionFailed", ex.Message, cancellationToken);
        }
    }
}
