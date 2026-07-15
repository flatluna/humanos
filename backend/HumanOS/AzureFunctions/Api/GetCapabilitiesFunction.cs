using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetCapabilitiesFunction
{
    private readonly CapabilityService _capabilityService;

    public GetCapabilitiesFunction(CapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("GetCapabilities")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "capabilities")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var language = query["language"] ?? "en";
        var domain = query["domain"];

        var capabilities = await _capabilityService.GetActiveAsync(language, domain, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, capabilities, cancellationToken: cancellationToken);
    }
}
