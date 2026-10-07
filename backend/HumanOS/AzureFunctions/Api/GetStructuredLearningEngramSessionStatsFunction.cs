using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// GET structured-learning/engram-sessions/{sessionId}/stats (2026-10-02)
/// — duration, per-step timing, Recall mastered/failed counts, Assessment
/// pass/fail counts, and token usage for ONE course-visit session. See
/// <see cref="StructuredLearningEngramStatsService"/>.
/// </summary>
public sealed class GetStructuredLearningEngramSessionStatsFunction
{
    private readonly StructuredLearningEngramStatsService _statsService;

    public GetStructuredLearningEngramSessionStatsFunction(StructuredLearningEngramStatsService statsService)
    {
        _statsService = statsService;
    }

    [Function("GetStructuredLearningEngramSessionStats")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "structured-learning/engram-sessions/{sessionId:guid}/stats")]
        HttpRequestData request,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var stats = await _statsService.GetSessionStatsAsync(sessionId, cancellationToken);
        if (stats is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SessionNotFound", $"StructuredLearningEngramSession {sessionId} not found.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, stats, cancellationToken: cancellationToken);
    }
}
