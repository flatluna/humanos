using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/monthly-costs — total estimated spend
/// per calendar month across every course this tenant created. See
/// <see cref="TenantMonthlyCostService"/>.</summary>
public sealed class GetTenantMonthlyCostsFunction
{
    private readonly TenantMonthlyCostService _monthlyCostService;

    public GetTenantMonthlyCostsFunction(TenantMonthlyCostService monthlyCostService)
    {
        _monthlyCostService = monthlyCostService;
    }

    [Function("GetTenantMonthlyCosts")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/monthly-costs")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var months = await _monthlyCostService.GetMonthlyCostsAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, months, cancellationToken: cancellationToken);
    }
}
