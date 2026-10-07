using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /api/usage/brain?from=yyyy-MM&amp;to=yyyy-MM — platform-wide
/// (ALL tenants) "Reactivar Engrama" (Brain spaced-repetition review) usage
/// bucketed by month. See <see cref="PlatformUsageDashboardService.GetBrainUsageMonthlyAsync"/>.</summary>
public sealed class GetPlatformBrainUsageMonthlyFunction
{
    private readonly PlatformUsageDashboardService _service;

    public GetPlatformBrainUsageMonthlyFunction(PlatformUsageDashboardService service)
    {
        _service = service;
    }

    [Function("GetPlatformBrainUsageMonthly")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "usage/brain")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var rows = await _service.GetBrainUsageMonthlyAsync(query["from"], query["to"], cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, rows, cancellationToken: cancellationToken);
    }
}
