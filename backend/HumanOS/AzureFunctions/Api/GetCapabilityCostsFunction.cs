using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>List view for the cost dashboard's cards.</summary>
public sealed class GetCapabilityCostsFunction
{
    private readonly CapabilityCostService _capabilityCostService;

    public GetCapabilityCostsFunction(CapabilityCostService capabilityCostService)
    {
        _capabilityCostService = capabilityCostService;
    }

    [Function("GetCapabilityCosts")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "studio/capability-costs")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        DateOnly? date = DateOnly.TryParse(query["date"], out var parsedDate) ? parsedDate : null;

        var summaries = await _capabilityCostService.GetSummariesAsync(date, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, summaries, cancellationToken: cancellationToken);
    }
}
