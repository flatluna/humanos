using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetTenantFunction
{
    private readonly TenantService _tenantService;

    public GetTenantFunction(TenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [Function("GetTenant")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "tenants/{tenantId:guid}")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantService.GetByIdAsync(
            tenantId,
            cancellationToken);

        if (tenant is null)
        {
            var notFoundResponse =
                request.CreateResponse(HttpStatusCode.NotFound);

            await notFoundResponse.WriteAsJsonAsync(
                new
                {
                    error = "TenantNotFound",
                    message = "The requested tenant was not found."
                },
                cancellationToken);

            return notFoundResponse;
        }

        var response = request.CreateResponse(HttpStatusCode.OK);

        await response.WriteAsJsonAsync(
            new
            {
                tenant.TenantId,
                tenant.Name,
                tenant.Slug,
                tenant.Domain,
                tenant.Description,
                tenant.CultureCode,
                tenant.TimeZone,
                tenant.IsActive,
                tenant.CreatedDate,
                tenant.UpdatedDate
            },
            cancellationToken);

        return response;
    }
}
