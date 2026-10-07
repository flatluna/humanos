using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /api/usage/agents?from=yyyy-MM&amp;to=yyyy-MM — platform-wide
/// (ALL tenants) in-course Q&amp;A-with-Professor agent/model usage bucketed
/// by month. See <see cref="PlatformUsageDashboardService.GetAgentUsageMonthlyAsync"/>.</summary>
public sealed class GetPlatformAgentUsageMonthlyFunction
{
    private readonly PlatformUsageDashboardService _service;

    public GetPlatformAgentUsageMonthlyFunction(PlatformUsageDashboardService service)
    {
        _service = service;
    }

    [Function("GetPlatformAgentUsageMonthly")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "usage/agents")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var rows = await _service.GetAgentUsageMonthlyAsync(query["from"], query["to"], cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, rows, cancellationToken: cancellationToken);
    }
}
