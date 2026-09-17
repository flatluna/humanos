using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// AI Agents chat window (2026-08-18) — one turn of the domain/topic-level
/// Q&amp;A agent at /ai-agents/:subject/:topic. Delegates retrieval +
/// grounded answer generation to StructuredLearningChatService.
/// </summary>
public sealed class PostStructuredLearningChatFunction
{
    private readonly StructuredLearningChatService _chatService;

    public PostStructuredLearningChatFunction(StructuredLearningChatService chatService)
    {
        _chatService = chatService;
    }

    public sealed class ChatHistoryEntryDto
    {
        public string Role { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;
    }

    public sealed class ChatRequest
    {
        public Guid TenantId { get; set; }

        public Guid? PersonId { get; set; }

        public string Message { get; set; } = string.Empty;

        public List<ChatHistoryEntryDto> History { get; set; } = [];

        /// <summary>Topic-level agent scope (e.g. "Matemáticas/Álgebra") — all
        /// courses under this Subject(+Topic).</summary>
        public Guid? SubjectId { get; set; }

        public Guid? TopicId { get; set; }

        /// <summary>Course-level agent scope — restricts retrieval to a
        /// single StructuredLearningDocument's own Engrams.</summary>
        public Guid? DocumentId { get; set; }

        /// <summary>Program-level agent scope — retrieves engrams across
        /// all courses belonging to this program.</summary>
        public Guid? ProgramId { get; set; }
    }

    [Function("PostStructuredLearningChat")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/chat")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var body = await JsonSerializer.DeserializeAsync<ChatRequest>(
            request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);

        if (body is null || string.IsNullOrWhiteSpace(body.Message)
            || (body.TenantId == Guid.Empty && body.DocumentId is null && body.SubjectId is null && body.TopicId is null && body.ProgramId is null))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
            request, HttpStatusCode.BadRequest, "InvalidRequest", "'message' and either 'tenantId', 'documentId', 'subjectId', 'topicId' or 'programId' are required.", cancellationToken);
        }

        if (!_chatService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotImplemented, "NotConfigured", "The AI Agents chat tutor is not configured.", cancellationToken);
        }

        var history = body.History
            .Select(h => new StructuredLearningChatHistoryEntry { Role = h.Role, Text = h.Text })
            .ToList();

        StructuredLearningChatResult result;
        try
        {
            result = await _chatService.AskAsync(
                body.TenantId, body.Message, history, cancellationToken, body.SubjectId, body.TopicId, body.DocumentId, body.PersonId, body.ProgramId);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Forbidden, "CourseNotAvailable", ex.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "ChatError", $"{ex.GetType().Name}: {ex.Message} \n {ex.StackTrace}", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new { result.Message, result.UsedEngrams },
            cancellationToken: cancellationToken);
    }
}
