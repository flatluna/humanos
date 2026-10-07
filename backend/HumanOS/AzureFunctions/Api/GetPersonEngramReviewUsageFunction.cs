using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// GET people/{personId}/engram-review-usage (2026-10-04) — real "Reactivar
/// Engrama" (Brain spaced-repetition review) spend for one person: total
/// sessions/duration/cost plus a per-node breakdown. Powers the admin
/// "Users" person detail page's "Brain" (repasos) tab. See
/// <see cref="StructuredLearningEngramStatsService.GetPersonEngramReviewUsageAsync"/>.
/// </summary>
public sealed class GetPersonEngramReviewUsageFunction
{
    private readonly StructuredLearningEngramStatsService _statsService;

    public GetPersonEngramReviewUsageFunction(StructuredLearningEngramStatsService statsService)
    {
        _statsService = statsService;
    }

    [Function("GetPersonEngramReviewUsage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/engram-review-usage")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var usage = await _statsService.GetPersonEngramReviewUsageAsync(personId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, usage, cancellationToken: cancellationToken);
    }
}
