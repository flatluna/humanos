using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class SubmitStructuredLearningDeepDiveAttemptRequest
{
    public string StudentResponse { get; set; } = string.Empty;

    /// <summary>The question shown to the student right before THIS
    /// attempt — omit only for a card's very first attempt.</summary>
    public string? TutorPromptShown { get; set; }
}

public sealed class SubmitStructuredLearningDeepDiveAttemptResponse
{
    public string Message { get; set; } = string.Empty;
    public int CardIndex { get; set; }
    public int TotalCards { get; set; }
    public bool Mastered { get; set; }
    public int NextCardIndex { get; set; }
    public bool Complete { get; set; }
}

/// <summary>
/// Real-time Deep Dive (2026-08-10) — the ONLY entry point for Deep Dive
/// card quizzing on a Structured Learning section. Scores one attempt on
/// the CURRENT card and reports whether to retry it, move to the next
/// card, or that the whole step is complete. Mirrors
/// TutorSubmitDeepDiveAttemptFunction's Graph counterpart — see
/// <see cref="StructuredLearningDeepDiveEngine"/>'s doc comment.
/// </summary>
public sealed class SubmitStructuredLearningDeepDiveAttemptFunction
{
    private readonly StructuredLearningDeepDiveEngine _engine;
    private readonly HumanOS.Agents.Studio.StructuredLearningDeepDiveAgent _agent;
    private readonly HumanOsDbContext _dbContext;

    public SubmitStructuredLearningDeepDiveAttemptFunction(
        StructuredLearningDeepDiveEngine engine, HumanOS.Agents.Studio.StructuredLearningDeepDiveAgent agent, HumanOsDbContext dbContext)
    {
        _engine = engine;
        _agent = agent;
        _dbContext = dbContext;
    }

    [Function("SubmitStructuredLearningDeepDiveAttempt")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/deepdive/attempt")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StructuredLearningDeepDiveAgentNotConfigured",
                "StructuredLearningDeepDiveAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        SubmitStructuredLearningDeepDiveAttemptRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SubmitStructuredLearningDeepDiveAttemptRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.StudentResponse))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "StudentResponse is required.", cancellationToken);
        }

        try
        {
            var result = await _engine.SubmitAttemptAsync(_dbContext, sectionId, body.StudentResponse, body.TutorPromptShown, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new SubmitStructuredLearningDeepDiveAttemptResponse
            {
                Message = result.Message,
                CardIndex = result.CardIndex,
                TotalCards = result.TotalCards,
                Mastered = result.Mastered,
                NextCardIndex = result.NextCardIndex,
                Complete = result.Complete
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidDeepDiveAttempt", ex.Message, cancellationToken);
        }
    }
}
