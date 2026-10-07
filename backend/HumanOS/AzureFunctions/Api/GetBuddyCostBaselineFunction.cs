using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /simulations/buddy-cost-baseline — the real, already-
/// configured per-unit $ rates for Buddy (Realtime voice minutes, text
/// input/output tokens at the gpt-5-mini rate) plus each quota's platform
/// default monthly limit (2026-10-04, Simulations wizard Step 3 — "Buddy at
/// max quota"). Reuses <see cref="TenantQuotaService.GetBuddyCostBaseline"/>
/// — the exact same rates/keys the admin "Quotas" tab's Buddy category
/// already shows, no new calculation.</summary>
public sealed class GetBuddyCostBaselineFunction
{
    private readonly TenantQuotaService _quotaService;

    public GetBuddyCostBaselineFunction(TenantQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    [Function("GetBuddyCostBaseline")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "simulations/buddy-cost-baseline")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var baseline = _quotaService.GetBuddyCostBaseline();
        return await FunctionResponseFactory.SuccessResponseAsync(request, baseline, cancellationToken: cancellationToken);
    }
}
