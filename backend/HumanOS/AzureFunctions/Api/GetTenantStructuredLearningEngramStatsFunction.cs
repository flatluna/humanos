using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/structured-learning-engram-stats —
/// per-engram session time, Recall/Assessment pass-fail counts, and token
/// usage across every Structured Learning course this tenant owns. See
/// <see cref="StructuredLearningEngramStatsService.GetTenantEngramStatsAsync"/>.</summary>
public sealed class GetTenantStructuredLearningEngramStatsFunction
{
    private readonly StructuredLearningEngramStatsService _statsService;

    public GetTenantStructuredLearningEngramStatsFunction(StructuredLearningEngramStatsService statsService)
    {
        _statsService = statsService;
    }

    [Function("GetTenantStructuredLearningEngramStats")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/structured-learning-engram-stats")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await _statsService.GetTenantEngramStatsAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, rows, cancellationToken: cancellationToken);
    }
}
