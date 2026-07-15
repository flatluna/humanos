using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetCapabilityDomainsFunction
{
    private readonly CapabilityDomainService _capabilityDomainService;

    public GetCapabilityDomainsFunction(CapabilityDomainService capabilityDomainService)
    {
        _capabilityDomainService = capabilityDomainService;
    }

    [Function("GetCapabilityDomains")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "capability-domains")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var languageCode = request.Query["language"] ?? "en";

        var domains = await _capabilityDomainService.GetAsync(languageCode, cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(domains, cancellationToken);

        return response;
    }
}
