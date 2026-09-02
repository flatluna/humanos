using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/responsibilities — lists the tenant's Key Responsibility catalog.</summary>
public sealed class GetResponsibilitiesFunction
{
    private readonly ResponsibilityService _responsibilityService;

    public GetResponsibilitiesFunction(ResponsibilityService responsibilityService)
    {
        _responsibilityService = responsibilityService;
    }

    [Function("GetResponsibilities")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/responsibilities")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var responsibilities = await _responsibilityService.GetByTenantAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, responsibilities, cancellationToken: cancellationToken);
    }
}
