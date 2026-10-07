using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Number of course-level sessions started by one person for one structured-learning document.</summary>
public sealed record StructuredLearningCourseSessionCountResponse(
    int SessionCount,
    Guid? EngramSessionId,
    DateTime? StartedDate);

public sealed class GetStructuredLearningCourseSessionCountFunction
{
    private readonly StructuredLearningEngramSessionService _sessionService;

    public GetStructuredLearningCourseSessionCountFunction(StructuredLearningEngramSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [Function("GetStructuredLearningCourseSessionCount")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/structured-learning/{documentId:guid}/course-sessions/count")]
        HttpRequestData request,
        Guid personId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (personId == Guid.Empty || documentId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidSessionCountRequest",
                "A valid personId and documentId are required.", cancellationToken);
        }

        var summary = await _sessionService.GetCourseSessionSummaryAsync(personId, documentId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new StructuredLearningCourseSessionCountResponse(
                summary.SessionCount,
                summary.EngramSessionId,
                summary.StartedDate),
            cancellationToken: cancellationToken);
    }
}