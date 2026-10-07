using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /simulations/course-cost-baseline — the real, already-
/// computed platform-wide unit costs per pedagogy stage (Teaching/Learn with
/// and without Realtime voice, Recall, Assessment, Production/"In your
/// role", Deep Dive image) used to seed the admin app's business-scenario
/// Simulations wizard (2026-10-04). Reuses <see cref="TenantQuotaService.GetCourseCostBaselineAsync"/>
/// — exactly the same numbers already shown on a tenant's "Quotas" tab, no
/// new calculation.</summary>
public sealed class GetCourseCostBaselineFunction
{
    private readonly TenantQuotaService _quotaService;

    public GetCourseCostBaselineFunction(TenantQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    [Function("GetCourseCostBaseline")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "simulations/course-cost-baseline")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var baseline = await _quotaService.GetCourseCostBaselineAsync(cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, baseline, cancellationToken: cancellationToken);
    }
}
