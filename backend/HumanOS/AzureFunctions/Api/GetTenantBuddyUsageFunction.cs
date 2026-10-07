using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/buddy-usage — total Buddy voice-session
/// time/estimated cost for this tenant's employees. See
/// <see cref="TenantBuddyUsageService"/>.</summary>
public sealed class GetTenantBuddyUsageFunction
{
    private readonly TenantBuddyUsageService _buddyUsageService;

    public GetTenantBuddyUsageFunction(TenantBuddyUsageService buddyUsageService)
    {
        _buddyUsageService = buddyUsageService;
    }

    [Function("GetTenantBuddyUsage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/buddy-usage")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var usage = await _buddyUsageService.GetBuddyUsageAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, usage, cancellationToken: cancellationToken);
    }
}
