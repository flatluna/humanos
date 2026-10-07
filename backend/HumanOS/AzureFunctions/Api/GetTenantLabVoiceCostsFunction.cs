using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/lab-voice-costs \u2014 total Realtime-voice agent time/cost
/// for this tenant's (or global) Labs. See <see cref="TenantLabVoiceCostService"/>.</summary>
public sealed class GetTenantLabVoiceCostsFunction
{
    private readonly TenantLabVoiceCostService _voiceCostService;

    public GetTenantLabVoiceCostsFunction(TenantLabVoiceCostService voiceCostService)
    {
        _voiceCostService = voiceCostService;
    }

    [Function("GetTenantLabVoiceCosts")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/lab-voice-costs")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var cost = await _voiceCostService.GetVoiceCostsAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, cost, cancellationToken: cancellationToken);
    }
}
