using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/lab-evaluation-costs \u2014 total AttemptEvaluationAgent
/// prompt in/out cost for this tenant's (or global) Labs. See
/// <see cref="TenantLabEvaluationCostService"/>.</summary>
public sealed class GetTenantLabEvaluationCostsFunction
{
    private readonly TenantLabEvaluationCostService _evaluationCostService;

    public GetTenantLabEvaluationCostsFunction(TenantLabEvaluationCostService evaluationCostService)
    {
        _evaluationCostService = evaluationCostService;
    }

    [Function("GetTenantLabEvaluationCosts")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/lab-evaluation-costs")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var cost = await _evaluationCostService.GetEvaluationCostsAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, cost, cancellationToken: cancellationToken);
    }
}
