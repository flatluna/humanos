using System.Net;
using System.Text.Json;
using HumanOS.Agents.Runtime;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Runtime V1, Paso 4 (2026-07-17) — Endpoint 6: EVALUATE ASSESSMENT.
/// Runs the LLM-based scoring of the Assessment evidence already submitted
/// for a node, via <see cref="AssessmentEvaluator.EvaluateAssessmentAsync"/>
/// — Passed is always computed deterministically (Score &gt;= 70) inside
/// that service, never trusted from the LLM directly.
/// </summary>
public sealed class RuntimeEvaluateAssessmentFunction
{
    private readonly AssessmentEvaluator _assessmentEvaluator;
    private readonly AssessmentEvaluatorAgent _assessmentEvaluatorAgent;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeEvaluateAssessmentFunction(
        AssessmentEvaluator assessmentEvaluator,
        AssessmentEvaluatorAgent assessmentEvaluatorAgent,
        HumanOsDbContext dbContext)
    {
        _assessmentEvaluator = assessmentEvaluator;
        _assessmentEvaluatorAgent = assessmentEvaluatorAgent;
        _dbContext = dbContext;
    }

    [Function("RuntimeEvaluateAssessment")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/nodes/evaluate")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_assessmentEvaluatorAgent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "AssessmentEvaluatorAgentNotConfigured",
                "AssessmentEvaluatorAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        EvaluateRuntimeAssessmentRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<EvaluateRuntimeAssessmentRequest>(
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
            var result = await _assessmentEvaluator.EvaluateAssessmentAsync(_dbContext, body.LearningSessionNodeId, cancellationToken);

            var dto = new RuntimeAssessmentResultDto
            {
                Score = result.AssessmentResult.Score,
                Passed = result.AssessmentResult.Passed,
                Feedback = result.AssessmentResult.Feedback ?? string.Empty
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, dto, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NodeNotEvaluable", ex.Message, cancellationToken);
        }
    }
}
