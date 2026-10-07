using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/course-totals — aggregate cost/time/
/// media/engram totals across every course this tenant created. See
/// <see cref="TenantCourseTotalsService"/>.</summary>
public sealed class GetTenantCourseTotalsFunction
{
    private readonly TenantCourseTotalsService _totalsService;

    public GetTenantCourseTotalsFunction(TenantCourseTotalsService totalsService)
    {
        _totalsService = totalsService;
    }

    [Function("GetTenantCourseTotals")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/course-totals")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var totals = await _totalsService.GetTotalsAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, totals, cancellationToken: cancellationToken);
    }
}
