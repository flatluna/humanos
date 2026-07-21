using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Runtime V1, Paso 4 (2026-07-17) — Endpoint 5: ADVANCE STEP.
/// Marks the current Active step Completed and moves to the next step in
/// the fixed Memory Paradox order, via
/// <see cref="InstructorRuntimeOrchestrator.AdvanceToNextStepAsync"/>.
/// Returns 409 Conflict (not 500) when already on Assessment — there is no
/// next step; the caller must go through Endpoint 6 (Evaluate) then
/// Endpoint 7 (Complete) instead.
/// </summary>
public sealed class RuntimeAdvanceStepFunction
{
    private readonly InstructorRuntimeOrchestrator _orchestrator;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeAdvanceStepFunction(InstructorRuntimeOrchestrator orchestrator, HumanOsDbContext dbContext)
    {
        _orchestrator = orchestrator;
        _dbContext = dbContext;
    }

    [Function("RuntimeAdvanceStep")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/steps/advance")]
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

        try
        {
            var nextStep = await _orchestrator.AdvanceToNextStepAsync(_dbContext, body.LearningSessionNodeId, cancellationToken);
            var stepDto = RuntimeGraphApiMappers.ToStepDto(nextStep);

            return await FunctionResponseFactory.SuccessResponseAsync(request, stepDto, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Already on the Assessment step"))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "AlreadyOnAssessment", ex.Message, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot advance away from Recall"))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "RecallRequiresGate", ex.Message, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NodeNotFound", ex.Message, cancellationToken);
        }
    }
}
