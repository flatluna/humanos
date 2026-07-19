using System.Net;
using System.Text.Json;
using HumanOS.Agents.Runtime;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Adaptive Assessment (2026-07-18) — Grades the student's answer to one
/// question, then either returns the next question THIS round, or — if
/// this was question 5 — closes out the round (Passed/Failed) and, if
/// Failed, auto-starts a brand-new round with 5 new questions in the SAME
/// response (mirrors submitRecallAttempt's existing auto-advance
/// precedent). On a Passed round, also writes a LearningAssessmentResult
/// row so GraphProgressionEngine keeps working unchanged.
/// </summary>
public sealed class RuntimeSubmitAssessmentAnswerFunction
{
    private readonly AdaptiveAssessmentEngine _engine;
    private readonly AdaptiveAssessmentAgent _agent;
    private readonly HumanOsDbContext _dbContext;

    public RuntimeSubmitAssessmentAnswerFunction(AdaptiveAssessmentEngine engine, AdaptiveAssessmentAgent agent, HumanOsDbContext dbContext)
    {
        _engine = engine;
        _agent = agent;
        _dbContext = dbContext;
    }

    [Function("RuntimeSubmitAssessmentAnswer")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/assessment/answer")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "AdaptiveAssessmentAgentNotConfigured",
                "AdaptiveAssessmentAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        SubmitAssessmentAnswerRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SubmitAssessmentAnswerRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.AssessmentQuestionId == Guid.Empty || string.IsNullOrWhiteSpace(body.StudentAnswer))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "AssessmentQuestionId and StudentAnswer are both required.", cancellationToken);
        }

        try
        {
            var result = await _engine.SubmitAnswerAsync(_dbContext, body.AssessmentQuestionId, body.StudentAnswer, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(
                request, AdaptiveAssessmentApiMappers.ToSubmitAnswerResponse(result), cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "QuestionNotAnswerable", ex.Message, cancellationToken);
        }
    }
}
