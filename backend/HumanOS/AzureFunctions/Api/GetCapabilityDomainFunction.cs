using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetCapabilityDomainFunction
{
    private readonly CapabilityDomainService _capabilityDomainService;

    public GetCapabilityDomainFunction(CapabilityDomainService capabilityDomainService)
    {
        _capabilityDomainService = capabilityDomainService;
    }

    [Function("GetCapabilityDomain")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "capability-domains/{code}")]
        HttpRequestData request,
        string code,
        CancellationToken cancellationToken)
    {
        var languageCode = request.Query["language"] ?? "en";

        var domain = await _capabilityDomainService.GetByCodeAsync(code, languageCode, cancellationToken);

        if (domain is null)
        {
            var notFound = request.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(
                new { error = "CapabilityDomainNotFound", message = "The requested capability domain was not found." },
                cancellationToken);
            return notFound;
        }

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(domain, cancellationToken);

        return response;
    }
}
