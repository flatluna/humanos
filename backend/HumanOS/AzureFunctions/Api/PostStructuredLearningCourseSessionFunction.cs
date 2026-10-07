using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class PostStructuredLearningCourseSessionRequest
{
    public Guid? PersonId { get; set; }
}

/// <summary>
/// POST structured-learning/documents/{documentId}/course-session (2026-10-02)
/// \u2014 called once when a student opens the COURSE overview page (before
/// picking any engram/section), so "entering the course" itself is counted
/// and timestamped, informing the student their session has started. Reuses
/// <see cref="StructuredLearningEngramSessionService"/> with a null section
/// id (that FK is already nullable) \u2014 no schema change needed.
/// </summary>
public sealed class PostStructuredLearningCourseSessionFunction
{
    private readonly StructuredLearningEngramSessionService _sessionService;

    public PostStructuredLearningCourseSessionFunction(StructuredLearningEngramSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [Function("PostStructuredLearningCourseSession")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "structured-learning/documents/{documentId:guid}/course-session")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        PostStructuredLearningCourseSessionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<PostStructuredLearningCourseSessionRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        var session = await _sessionService.StartSessionAsync(body?.PersonId, documentId, sectionId: null, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new PostStructuredLearningEngramSessionResponse { EngramSessionId = session.StructuredLearningEngramSessionId, StartedDate = session.StartedDate },
            cancellationToken: cancellationToken);
    }
}
