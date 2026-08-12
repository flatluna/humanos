using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class SubmitStructuredLearningAssessmentAnswerRequest
{
    public string StudentAnswer { get; set; } = string.Empty;
}

public sealed class StructuredLearningAssessmentAnswerGradeDto
{
    public string Correctness { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Feedback { get; set; } = string.Empty;
}

public sealed class SubmitStructuredLearningAssessmentAnswerResponse
{
    public StructuredLearningAssessmentAnswerGradeDto Grade { get; set; } = null!;
    public bool RoundComplete { get; set; }
    public bool? Passed { get; set; }
    public int? FinalScore { get; set; }
    public StructuredLearningAssessmentQuestionDto? NextQuestion { get; set; }
    public int? NewRoundNumber { get; set; }
    public Guid? NewStructuredLearningAssessmentRoundId { get; set; }
}

/// <summary>
/// Real-time Assessment (2026-08-10) — grades the student's answer to one
/// question, then either returns the next question THIS round, or — if
/// this was the last question — closes out the round (Passed/Failed) and,
/// if Failed, auto-starts a brand-new round with fresh questions in the
/// SAME response. See <see cref="StructuredLearningAssessmentEngine"/>'s
/// doc comment.
/// </summary>
public sealed class SubmitStructuredLearningAssessmentAnswerFunction
{
    private readonly StructuredLearningAssessmentEngine _engine;
    private readonly HumanOS.Agents.Studio.StructuredLearningAssessmentAgent _agent;
    private readonly HumanOsDbContext _dbContext;

    public SubmitStructuredLearningAssessmentAnswerFunction(
        StructuredLearningAssessmentEngine engine, HumanOS.Agents.Studio.StructuredLearningAssessmentAgent agent, HumanOsDbContext dbContext)
    {
        _engine = engine;
        _agent = agent;
        _dbContext = dbContext;
    }

    [Function("SubmitStructuredLearningAssessmentAnswer")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/assessment/questions/{questionAttemptId:guid}/answer")]
        HttpRequestData request,
        Guid questionAttemptId,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StructuredLearningAssessmentAgentNotConfigured",
                "StructuredLearningAssessmentAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        SubmitStructuredLearningAssessmentAnswerRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SubmitStructuredLearningAssessmentAnswerRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.StudentAnswer))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "StudentAnswer is required.", cancellationToken);
        }

        try
        {
            var result = await _engine.SubmitAnswerAsync(_dbContext, questionAttemptId, body.StudentAnswer, cancellationToken);
            var response = new SubmitStructuredLearningAssessmentAnswerResponse
            {
                Grade = new StructuredLearningAssessmentAnswerGradeDto
                {
                    Correctness = result.Grade.Correctness.ToString(),
                    Score = result.Grade.Score,
                    Feedback = result.Grade.Feedback
                },
                RoundComplete = result.RoundComplete,
                Passed = result.Passed,
                FinalScore = result.FinalScore,
                NextQuestion = result.NextQuestion is null ? null : new StructuredLearningAssessmentQuestionDto
                {
                    StructuredLearningAssessmentQuestionAttemptId = result.NextQuestion.StructuredLearningAssessmentQuestionAttemptId,
                    QuestionIndex = result.NextQuestion.QuestionIndex,
                    QuestionText = result.NextQuestion.QuestionText,
                    Options = result.NextQuestion.Options
                },
                NewRoundNumber = result.NewRoundNumber,
                NewStructuredLearningAssessmentRoundId = result.NewStructuredLearningAssessmentRoundId
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidQuestion", ex.Message, cancellationToken);
        }
    }
}
