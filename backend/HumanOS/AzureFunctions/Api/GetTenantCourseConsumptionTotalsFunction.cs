using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/course-consumption-totals — aggregate
/// "courses taken" totals (sessions, tokens, realtime voice, text-to-speech,
/// Recall/Assessment pass-fail, estimated cost) across EVERY engram this
/// tenant's employees have ever visited. See
/// <see cref="StructuredLearningEngramStatsService.GetTenantCourseConsumptionTotalsAsync"/>.
/// Complements <see cref="GetTenantCourseTotalsFunction"/> which covers the
/// "courses created" (generation) side.</summary>
public sealed class GetTenantCourseConsumptionTotalsFunction
{
    private readonly StructuredLearningEngramStatsService _statsService;

    public GetTenantCourseConsumptionTotalsFunction(StructuredLearningEngramStatsService statsService)
    {
        _statsService = statsService;
    }

    [Function("GetTenantCourseConsumptionTotals")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/course-consumption-totals")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var totals = await _statsService.GetTenantCourseConsumptionTotalsAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, totals, cancellationToken: cancellationToken);
    }
}
