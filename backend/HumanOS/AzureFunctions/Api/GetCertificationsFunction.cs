using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/certifications — lists the tenant's Certification catalog.</summary>
public sealed class GetCertificationsFunction
{
    private readonly CertificationService _certificationService;

    public GetCertificationsFunction(CertificationService certificationService)
    {
        _certificationService = certificationService;
    }

    [Function("GetCertifications")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/certifications")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var certs = await _certificationService.GetByTenantAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, certs, cancellationToken: cancellationToken);
    }
}
