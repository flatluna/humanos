using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/lab-costs — one row per lab (owned by
/// this tenant or global) with name + estimated generation cost. See
/// <see cref="TenantLabCostsService"/>.</summary>
public sealed class GetTenantLabCostsFunction
{
    private readonly TenantLabCostsService _labCostsService;

    public GetTenantLabCostsFunction(TenantLabCostsService labCostsService)
    {
        _labCostsService = labCostsService;
    }

    [Function("GetTenantLabCosts")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/lab-costs")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var costs = await _labCostsService.GetLabCostsAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, costs, cancellationToken: cancellationToken);
    }
}
