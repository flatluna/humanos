using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /api/usage/courses-taken?from=yyyy-MM&amp;to=yyyy-MM —
/// platform-wide (ALL tenants) "courses taken" (consumption) totals
/// bucketed by month. See
/// <see cref="PlatformUsageDashboardService.GetCoursesTakenMonthlyAsync"/>.</summary>
public sealed class GetPlatformCoursesTakenMonthlyFunction
{
    private readonly PlatformUsageDashboardService _service;

    public GetPlatformCoursesTakenMonthlyFunction(PlatformUsageDashboardService service)
    {
        _service = service;
    }

    [Function("GetPlatformCoursesTakenMonthly")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "usage/courses-taken")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var rows = await _service.GetCoursesTakenMonthlyAsync(query["from"], query["to"], cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, rows, cancellationToken: cancellationToken);
    }
}
