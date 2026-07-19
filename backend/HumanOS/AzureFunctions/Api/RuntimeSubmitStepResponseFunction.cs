using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Runtime V1, Paso 4 (2026-07-17) — Endpoint 4: SUBMIT RESPONSE.
/// Persists what the person did/answered during a step as append-only
/// LearningEvidence, via <see cref="InstructorRuntimeOrchestrator.SubmitResponseAsync"/>.
/// Does not evaluate it — evaluation only happens for Assessment, via
/// Endpoint 6.
/// </summary>
public sealed class RuntimeSubmitStepResponseFunction
{
    private readonly InstructorRuntimeOrchestrator _orchestrator;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeSubmitStepResponseFunction(InstructorRuntimeOrchestrator orchestrator, HumanOsDbContext dbContext)
    {
        _orchestrator = orchestrator;
        _dbContext = dbContext;
    }

    [Function("RuntimeSubmitStepResponse")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/steps/respond")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        SubmitRuntimeStepResponseRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<SubmitRuntimeStepResponseRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.LearningSessionStepId == Guid.Empty || string.IsNullOrWhiteSpace(body.Response))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "LearningSessionStepId and Response are both required.", cancellationToken);
        }

        try
        {
            var learningEvidenceId = await _orchestrator.SubmitResponseAsync(
                _dbContext, body.LearningSessionStepId, body.Response, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, new { success = true, learningEvidenceId }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "StepNotFound", ex.Message, cancellationToken);
        }
    }
}
