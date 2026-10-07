using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class PostStructuredLearningEngramSessionRequest
{
    public Guid? PersonId { get; set; }

    public Guid? StructuredLearningDocumentId { get; set; }

    /// <summary>"BrainReview" when launched from the Brain mastered-engrams
    /// dashboard instead of the course page — null/omitted for the normal
    /// in-course flow. See <see cref="HumanOS.Models.Capabilities.Studio.StructuredLearningEngramSession.Source"/>.</summary>
    public string? Source { get; set; }
}

public sealed class PostStructuredLearningEngramSessionResponse
{
    public Guid EngramSessionId { get; set; }

    /// <summary>Server-recorded UTC entry time — shown to the student so the
    /// session's real start time (not the browser's own clock) is always visible.</summary>
    public DateTime StartedDate { get; set; }
}

/// <summary>
/// POST structured-learning/sections/{sectionId}/engram-session (2026-10-02)
/// — called once each time a student opens an engram, so every visit is
/// counted and timestamped. Returns the new session id the frontend then
/// attaches to every Voice/Text Professor usage report and step start/
/// complete event for this visit. See
/// <see cref="StructuredLearningEngramSessionService"/>.
/// </summary>
public sealed class PostStructuredLearningEngramSessionFunction
{
    private readonly StructuredLearningEngramSessionService _sessionService;

    public PostStructuredLearningEngramSessionFunction(StructuredLearningEngramSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [Function("PostStructuredLearningEngramSession")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "structured-learning/sections/{sectionId:guid}/engram-session")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        PostStructuredLearningEngramSessionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<PostStructuredLearningEngramSessionRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        var session = await _sessionService.StartSessionAsync(
            body?.PersonId, body?.StructuredLearningDocumentId, sectionId, cancellationToken, body?.Source);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new PostStructuredLearningEngramSessionResponse { EngramSessionId = session.StructuredLearningEngramSessionId, StartedDate = session.StartedDate },
            cancellationToken: cancellationToken);
    }
}
