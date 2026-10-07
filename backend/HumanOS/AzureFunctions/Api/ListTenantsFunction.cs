using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace HumanOS.AzureFunctions.Api;

public sealed class ListTenantsFunction
{
    private readonly TenantService _tenantService;

    public ListTenantsFunction(TenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [Function("ListTenants")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var tenants = await _tenantService.GetAllAsync(cancellationToken);
        var response = request.CreateResponse(HttpStatusCode.OK);

        await response.WriteAsJsonAsync(
            tenants.Select(tenant => new
            {
                tenant.TenantId,
                tenant.Name,
                tenant.Slug,
                tenant.Domain,
                tenant.OrgType,
                tenant.IsActive,
                tenant.CreatedDate,
            }),
            cancellationToken);

        return response;
    }
}