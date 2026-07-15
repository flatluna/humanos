using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Full read-only content (levels/modules/scripts/metrics) of a published
/// capability — used by the frontend's "view real generated content"
/// screen (distinct from GetCapabilityFunction, which only returns the
/// lightweight list/summary shape).
/// </summary>
public sealed class GetCapabilityContentFunction
{
    private readonly CapabilityService _capabilityService;

    public GetCapabilityContentFunction(CapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("GetCapabilityContent")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "capabilities/{capabilityId:guid}/content")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var content = await _capabilityService.GetContentByIdAsync(capabilityId, cancellationToken);

        if (content is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "CapabilityNotFound",
                "The requested capability was not found.",
                cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, content, cancellationToken: cancellationToken);
    }
}
