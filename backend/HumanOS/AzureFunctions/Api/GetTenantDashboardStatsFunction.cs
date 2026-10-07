using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/dashboard-stats — aggregate read-only
/// stats for one tenant's admin detail page (people count, course counts,
/// labs count, media counts). See <see cref="HumanOS.Contracts.Tenancy.TenantDashboardStatsResponse"/>.</summary>
public sealed class GetTenantDashboardStatsFunction
{
    private readonly TenantDashboardStatsService _statsService;

    public GetTenantDashboardStatsFunction(TenantDashboardStatsService statsService)
    {
        _statsService = statsService;
    }

    [Function("GetTenantDashboardStats")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/dashboard-stats")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var stats = await _statsService.GetStatsAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, stats, cancellationToken: cancellationToken);
    }
}
