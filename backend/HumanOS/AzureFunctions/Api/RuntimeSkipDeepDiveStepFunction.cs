using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Models.Learning;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// POST /instructor-runtime/steps/deepdive-skip — 2026-08-05, explicit
/// repeated user request: "no quiero forzarlo a contestar perfecto...
/// quiero que escuche, converse, pero que decida cuando dar next... aunque
/// no haya enviado respuesta... siguiente tarjeta siempre tiene que estar
/// disponible". Deep Dive is meant to be low-stakes practice, not a gate —
/// unlike Recall/Assessment, a student must always be able to move past
/// EVERY card (including the last one) without ever answering it.
///
/// Self-service (any signed-in student can call this on THEIR OWN active
/// session — same trust model as the other public /instructor-runtime/
/// steps/* endpoints, e.g. RuntimeAdvanceStepFunction, which also take no
/// caller-identity headers). Unlike HrForceAdvanceStepFunction (which lets
/// an HRAdmin force-advance someone ELSE's session and is gated by a real
/// server-side OrgRole/tenant check), this endpoint needs no such check —
/// it only ever acts on the session the caller already owns/holds the
/// LearningSessionNodeId for.
///
/// Scoped to ONLY an Active DeepDive step (409 otherwise) — this can never
/// be used to bypass Recall/Production/Assessment's real mastery gates,
/// which stay exactly as strict as before.
/// </summary>
public sealed class RuntimeSkipDeepDiveStepFunction
{
    private readonly InstructorRuntimeOrchestrator _orchestrator;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeSkipDeepDiveStepFunction(InstructorRuntimeOrchestrator orchestrator, HumanOsDbContext dbContext)
    {
        _orchestrator = orchestrator;
        _dbContext = dbContext;
    }

    [Function("RuntimeSkipDeepDiveStep")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/steps/deepdive-skip")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        AdvanceRuntimeStepRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<AdvanceRuntimeStepRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.LearningSessionNodeId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "LearningSessionNodeId is required.", cancellationToken);
        }

        var sessionNode = await _dbContext.LearningSessionNodes
            .Include(n => n.Steps)
            .FirstOrDefaultAsync(n => n.LearningSessionNodeId == body.LearningSessionNodeId, cancellationToken);

        if (sessionNode is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NodeNotFound",
                $"No LearningSessionNode found with id {body.LearningSessionNodeId}.", cancellationToken);
        }

        var activeStep = sessionNode.Steps.SingleOrDefault(s => s.Status == LearningSessionStepStatus.Active);
        if (activeStep is null || activeStep.StepType != ExperienceStepType.DeepDive)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "NotDeepDive",
                "This endpoint can only skip an Active Deep Dive step.", cancellationToken);
        }

        try
        {
            var nextStep = await _orchestrator.AdvanceToNextStepAsync(
                _dbContext, body.LearningSessionNodeId, cancellationToken, bypassGatedStepGuard: true);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, RuntimeGraphApiMappers.ToStepDto(nextStep), cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "AdvanceFailed", ex.Message, cancellationToken);
        }
    }
}
