using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetCapabilityFunction
{
    private readonly CapabilityService _capabilityService;

    public GetCapabilityFunction(CapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("GetCapability")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "capabilities/{code}")]
        HttpRequestData request,
        string code,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var language = query["language"] ?? "en";

        var capability = await _capabilityService.GetByCodeAsync(code, language, cancellationToken);

        if (capability is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "CapabilityNotFound",
                "The requested capability was not found.",
                cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, capability, cancellationToken: cancellationToken);
    }
}
