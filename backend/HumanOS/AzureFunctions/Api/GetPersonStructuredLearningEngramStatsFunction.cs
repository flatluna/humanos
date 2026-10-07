using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /people/{personId}/structured-learning-engram-stats —
/// per-engram session time, Recall/Assessment pass-fail counts, voice/
/// production/Azure Speech usage for every course this ONE person has
/// activity in, in a single batched round-trip. See
/// <see cref="StructuredLearningEngramStatsService.GetPersonEngramStatsAsync"/>.
/// Replaces the admin app's old N+1 pattern (one document fetch + one
/// stats fetch per section, per enrolled course) for the "Brain" tab.</summary>
public sealed class GetPersonStructuredLearningEngramStatsFunction
{
    private readonly StructuredLearningEngramStatsService _statsService;

    public GetPersonStructuredLearningEngramStatsFunction(StructuredLearningEngramStatsService statsService)
    {
        _statsService = statsService;
    }

    [Function("GetPersonStructuredLearningEngramStats")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/structured-learning-engram-stats")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var rows = await _statsService.GetPersonEngramStatsAsync(personId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, rows, cancellationToken: cancellationToken);
    }
}
