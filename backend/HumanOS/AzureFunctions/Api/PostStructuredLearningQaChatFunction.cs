using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

public sealed class PostStructuredLearningQaChatRequest
{
    public Guid? PersonId { get; set; }

    public string Message { get; set; } = string.Empty;

    public List<StructuredLearningQaChatHistoryEntry> History { get; set; } = [];

    /// <summary>DeepDive cards only (2026-10-02) — which ExampleCard the
    /// student is viewing, so the text Professor explains that one card
    /// instead of the whole section (mirrors the Voice tab's reviewOnly
    /// card path). Null for every other step type.</summary>
    public int? ExampleCardIndex { get; set; }

    /// <summary>The real engram visit (StructuredLearningEngramSession) this
    /// turn happened during (2026-10-02) — token usage is accumulated under
    /// this session instead of a lifetime-per-person bucket. Null if the
    /// frontend hasn't started a session yet.</summary>
    public Guid? EngramSessionId { get; set; }
}

public sealed class PostStructuredLearningQaChatResponse
{
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Text tab of "Q&amp;A with Professor" (2026-10-01) — cheap, non-voice
/// sibling of VoiceTutorSessionFunction's Q&amp;A mode. Same grounding
/// (section content + full LEARNER CONTEXT incl. the student's Hypothesis
/// answer), just a plain request/response turn instead of a Realtime
/// WebRTC session, so students who don't need audio don't pay voice costs.
/// </summary>
public sealed class PostStructuredLearningQaChatFunction
{
    private readonly StructuredLearningQaProfessorChatService _chatService;
    private readonly ILogger<PostStructuredLearningQaChatFunction> _logger;

    public PostStructuredLearningQaChatFunction(
        StructuredLearningQaProfessorChatService chatService,
        ILogger<PostStructuredLearningQaChatFunction> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [Function("PostStructuredLearningQaChat")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "structured-learning/sections/steps/{stepId:guid}/qa-chat")]
        HttpRequestData request,
        Guid stepId,
        CancellationToken cancellationToken)
    {
        if (!_chatService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "QaProfessorChatNotConfigured",
                "The Q&A Professor text agent is not configured. Verify Azure OpenAI credentials.",
                cancellationToken);
        }

        PostStructuredLearningQaChatRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<PostStructuredLearningQaChatRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Message))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "Message is required.", cancellationToken);
        }

        try
        {
            var answer = await _chatService.AskAsync(stepId, body.PersonId, body.Message, body.History, cancellationToken, body.ExampleCardIndex, body.EngramSessionId);
            return await FunctionResponseFactory.SuccessResponseAsync(
                request, new PostStructuredLearningQaChatResponse { Message = answer }, cancellationToken: cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "StepNotFound", ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to answer Q&A Professor text chat for step {StepId}", stepId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "QaProfessorChatFailed", ex.Message, cancellationToken);
        }
    }
}
