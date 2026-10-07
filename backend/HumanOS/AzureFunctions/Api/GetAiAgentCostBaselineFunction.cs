using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /simulations/ai-agent-cost-baseline — the real, already-
/// computed cost of the "AI Agents"/"Grow With Us" per-course chat agent
/// (2026-10-04, Simulations wizard Step 5): average $/text message
/// (platform-wide, from real recorded StepType="AiAgentChat" usage) plus
/// the flat Realtime voice $/minute rate. Reuses
/// <see cref="TenantQuotaService.GetAiAgentCostBaselineAsync"/> — no new
/// calculation.</summary>
public sealed class GetAiAgentCostBaselineFunction
{
    private readonly TenantQuotaService _quotaService;

    public GetAiAgentCostBaselineFunction(TenantQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    [Function("GetAiAgentCostBaseline")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "simulations/ai-agent-cost-baseline")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var baseline = await _quotaService.GetAiAgentCostBaselineAsync(cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, baseline, cancellationToken: cancellationToken);
    }
}
