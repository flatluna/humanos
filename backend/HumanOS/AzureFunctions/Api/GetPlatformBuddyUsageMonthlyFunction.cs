using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /api/usage/buddy?from=yyyy-MM&amp;to=yyyy-MM — platform-wide
/// (ALL tenants) Buddy realtime-voice usage bucketed by month. See
/// <see cref="PlatformUsageDashboardService.GetBuddyUsageMonthlyAsync"/>.</summary>
public sealed class GetPlatformBuddyUsageMonthlyFunction
{
    private readonly PlatformUsageDashboardService _service;

    public GetPlatformBuddyUsageMonthlyFunction(PlatformUsageDashboardService service)
    {
        _service = service;
    }

    [Function("GetPlatformBuddyUsageMonthly")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "usage/buddy")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var rows = await _service.GetBuddyUsageMonthlyAsync(query["from"], query["to"], cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, rows, cancellationToken: cancellationToken);
    }
}
