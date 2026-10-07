using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// POST structured-learning/engram-sessions/{sessionId}/close (2026-10-02)
/// — called when the student presses "Exit course". Sets CompletedDate
/// and the real total DurationSeconds across every engram visited this
/// session. See <see cref="StructuredLearningEngramSessionService"/>.
/// </summary>
public sealed class PostStructuredLearningEngramSessionCloseFunction
{
    private readonly StructuredLearningEngramSessionService _sessionService;

    public PostStructuredLearningEngramSessionCloseFunction(StructuredLearningEngramSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [Function("PostStructuredLearningEngramSessionClose")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "structured-learning/engram-sessions/{sessionId:guid}/close")]
        HttpRequestData request,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await _sessionService.CloseSessionAsync(sessionId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, new { logged = true }, cancellationToken: cancellationToken);
    }
}
