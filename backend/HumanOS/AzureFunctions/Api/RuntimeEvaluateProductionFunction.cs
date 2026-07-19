using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Production ("Aplícalo") formative evaluation (2026-07-18) — grades one
/// submission via ProductionEvaluatorAgent/ProductionEvaluationGate and
/// persists it as LearningEvidence. Purely formative: never writes
/// LearningAssessmentResult, never affects node mastery/unlocking, and
/// never advances the step itself — the frontend calls the existing
/// /instructor-runtime/steps/advance endpoint separately, only after
/// showing the student an IsCorrect=true verdict. On IsCorrect=false the
/// student may resubmit as many times as they want — no attempt cap (see
/// ProductionEvaluationGate).
/// </summary>
public sealed class RuntimeEvaluateProductionFunction
{
    private readonly ProductionEvaluationService _evaluationService;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeEvaluateProductionFunction(ProductionEvaluationService evaluationService, HumanOsDbContext dbContext)
    {
        _evaluationService = evaluationService;
        _dbContext = dbContext;
    }

    [Function("RuntimeEvaluateProduction")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/production/evaluate")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_evaluationService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "ProductionEvaluatorAgentNotConfigured",
                "ProductionEvaluatorAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        EvaluateProductionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<EvaluateProductionRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.LearningSessionStepId == Guid.Empty || string.IsNullOrWhiteSpace(body.StudentSubmission))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "LearningSessionStepId and StudentSubmission are both required.", cancellationToken);
        }

        try
        {
            var outcome = await _evaluationService.EvaluateAsync(
                _dbContext, body.LearningSessionStepId, body.StudentSubmission, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, ProductionApiMappers.ToDto(outcome), cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "StepNotEvaluable", ex.Message, cancellationToken);
        }
    }
}
