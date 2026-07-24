using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Detail view for one Capability's expanded card in the cost dashboard.</summary>
public sealed class GetCapabilityCostDetailFunction
{
    private readonly CapabilityCostService _capabilityCostService;

    public GetCapabilityCostDetailFunction(CapabilityCostService capabilityCostService)
    {
        _capabilityCostService = capabilityCostService;
    }

    [Function("GetCapabilityCostDetail")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "studio/capability-costs/{capabilityId:guid}")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var detail = await _capabilityCostService.GetDetailAsync(capabilityId, cancellationToken);
        if (detail is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "not_found",
                "No se encontró esta capability.",
                cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, detail, cancellationToken: cancellationToken);
    }
}
