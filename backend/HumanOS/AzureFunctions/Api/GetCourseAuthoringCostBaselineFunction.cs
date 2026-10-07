using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /simulations/course-authoring-cost-baseline — the real,
/// already-computed platform-wide unit costs for DESIGNING a course in
/// HumanOS Studio (Extraction of entities/facts, KnowledgeGraph structuring,
/// Pedagogy lesson writing, Deep Dive images), per Engram (2026-10-04,
/// Simulations wizard Step 1 — "Create courses"). Distinct from
/// <see cref="GetCourseCostBaselineFunction"/>, which prices a STUDENT
/// actually taking the finished course (Step 2). Reuses
/// <see cref="TenantQuotaService.GetCourseAuthoringCostBaselineAsync"/> — no
/// new calculation.</summary>
public sealed class GetCourseAuthoringCostBaselineFunction
{
    private readonly TenantQuotaService _quotaService;

    public GetCourseAuthoringCostBaselineFunction(TenantQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    [Function("GetCourseAuthoringCostBaseline")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "simulations/course-authoring-cost-baseline")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var baseline = await _quotaService.GetCourseAuthoringCostBaselineAsync(cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, baseline, cancellationToken: cancellationToken);
    }
}
