using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Runtime V1, Paso 4 (2026-07-17) — Endpoint 1: START SESSION.
/// Starts a brand-new LearningSession for a person on a Capability's node
/// (via <see cref="InstructorRuntimeOrchestrator.StartSessionAsync"/>), then
/// immediately resolves its first (Hypothesis) step's content/illustrations
/// (via <see cref="InstructorRuntimeOrchestrator.GetCurrentStepAsync"/>) so
/// the UI can render straight away without a second round-trip.
/// </summary>
public sealed class RuntimeStartSessionFunction
{
    private readonly InstructorRuntimeOrchestrator _orchestrator;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeStartSessionFunction(InstructorRuntimeOrchestrator orchestrator, HumanOsDbContext dbContext)
    {
        _orchestrator = orchestrator;
        _dbContext = dbContext;
    }

    [Function("RuntimeStartSession")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/sessions/start")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        StartRuntimeGraphSessionRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<StartRuntimeGraphSessionRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.PersonId == Guid.Empty || body.CapabilityId == Guid.Empty || body.CapabilityGraphNodeId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "PersonId, CapabilityId and CapabilityGraphNodeId are all required.", cancellationToken);
        }

        try
        {
            var startResult = await _orchestrator.StartSessionAsync(
                _dbContext, body.PersonId, body.CapabilityId, body.CapabilityGraphNodeId, cancellationToken);

            var currentStep = await _orchestrator.GetCurrentStepAsync(
                _dbContext, startResult.LearningSessionNodeId, cancellationToken);

            var sessionInfo = RuntimeGraphApiMappers.ToSessionInfo(
                startResult.LearningSessionId, startResult.LearningSessionNodeId, startResult.CapabilityGraphNodeId, currentStep);

            return await FunctionResponseFactory.CreatedResponseAsync(request, sessionInfo, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NoBlueprintFound", ex.Message, cancellationToken);
        }
    }
}
