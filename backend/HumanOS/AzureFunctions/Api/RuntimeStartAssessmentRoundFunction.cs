using System.Net;
using System.Text.Json;
using HumanOS.Agents.Runtime;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Adaptive Assessment (2026-07-18) — Starts a brand-new dynamic Assessment
/// round (5 questions, one at a time) for a node and generates its first
/// question. Called by the UI the first time a student reaches the
/// Assessment step (when GET assessment/active returns null).
/// </summary>
public sealed class RuntimeStartAssessmentRoundFunction
{
    private readonly AdaptiveAssessmentEngine _engine;
    private readonly AdaptiveAssessmentAgent _agent;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeStartAssessmentRoundFunction(AdaptiveAssessmentEngine engine, AdaptiveAssessmentAgent agent, HumanOsDbContext dbContext)
    {
        _engine = engine;
        _agent = agent;
        _dbContext = dbContext;
    }

    [Function("RuntimeStartAssessmentRound")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/assessment/start-round")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "AdaptiveAssessmentAgentNotConfigured",
                "AdaptiveAssessmentAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        StartAssessmentRoundRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<StartAssessmentRoundRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
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
            var round = await _engine.StartRoundAsync(_dbContext, body.LearningSessionNodeId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(
                request, AdaptiveAssessmentApiMappers.ToRoundStateDto(round), cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NodeNotFound", ex.Message, cancellationToken);
        }
    }
}
