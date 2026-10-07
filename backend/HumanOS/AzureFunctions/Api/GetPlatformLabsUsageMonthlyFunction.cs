using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /api/usage/labs?from=yyyy-MM&amp;to=yyyy-MM — platform-wide
/// (ALL tenants) Labs (simulation) attempts/voice/evaluation usage bucketed
/// by month. See <see cref="PlatformUsageDashboardService.GetLabsUsageMonthlyAsync"/>.</summary>
public sealed class GetPlatformLabsUsageMonthlyFunction
{
    private readonly PlatformUsageDashboardService _service;

    public GetPlatformLabsUsageMonthlyFunction(PlatformUsageDashboardService service)
    {
        _service = service;
    }

    [Function("GetPlatformLabsUsageMonthly")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "usage/labs")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var rows = await _service.GetLabsUsageMonthlyAsync(query["from"], query["to"], cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, rows, cancellationToken: cancellationToken);
    }
}
