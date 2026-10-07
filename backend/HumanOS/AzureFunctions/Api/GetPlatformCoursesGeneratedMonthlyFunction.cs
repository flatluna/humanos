using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /api/usage/courses-generated?from=yyyy-MM&amp;to=yyyy-MM —
/// platform-wide (ALL tenants) "courses created" totals bucketed by month.
/// See <see cref="PlatformUsageDashboardService.GetCoursesGeneratedMonthlyAsync"/>.</summary>
public sealed class GetPlatformCoursesGeneratedMonthlyFunction
{
    private readonly PlatformUsageDashboardService _service;

    public GetPlatformCoursesGeneratedMonthlyFunction(PlatformUsageDashboardService service)
    {
        _service = service;
    }

    [Function("GetPlatformCoursesGeneratedMonthly")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "usage/courses-generated")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var rows = await _service.GetCoursesGeneratedMonthlyAsync(query["from"], query["to"], cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, rows, cancellationToken: cancellationToken);
    }
}
