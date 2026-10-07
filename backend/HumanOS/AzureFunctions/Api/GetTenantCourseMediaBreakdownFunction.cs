using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/media-breakdown — top courses created
/// by this tenant ranked by total image+video count, for the dashboard's
/// media histogram. See <see cref="TenantCourseMediaBreakdownService"/>.</summary>
public sealed class GetTenantCourseMediaBreakdownFunction
{
    private readonly TenantCourseMediaBreakdownService _mediaBreakdownService;

    public GetTenantCourseMediaBreakdownFunction(TenantCourseMediaBreakdownService mediaBreakdownService)
    {
        _mediaBreakdownService = mediaBreakdownService;
    }

    [Function("GetTenantCourseMediaBreakdown")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/media-breakdown")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var courses = await _mediaBreakdownService.GetTopCoursesAsync(tenantId, 20, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, courses, cancellationToken: cancellationToken);
    }
}
