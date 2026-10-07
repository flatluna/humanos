using System.Web;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// GET structured-learning/sections/{sectionId}/stats (2026-10-02) —
/// stats for ONE engram aggregated across every session/student that
/// ever visited it, or scoped to one person via ?personId= (2026-10-03,
/// powers the admin "per user" course dashboard). See
/// <see cref="StructuredLearningEngramStatsService"/>.
/// </summary>
public sealed class GetStructuredLearningEngramStatsFunction
{
    private readonly StructuredLearningEngramStatsService _statsService;

    public GetStructuredLearningEngramStatsFunction(StructuredLearningEngramStatsService statsService)
    {
        _statsService = statsService;
    }

    [Function("GetStructuredLearningEngramStats")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "structured-learning/sections/{sectionId:guid}/stats")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(request.Url.Query);
        Guid? personId = Guid.TryParse(query["personId"], out var parsedPersonId) ? parsedPersonId : null;

        var stats = await _statsService.GetEngramStatsAsync(sectionId, personId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, stats, cancellationToken: cancellationToken);
    }
}
