using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetTenantInvitationsFunction
{
    private readonly TenantInvitationService _invitationService;

    public GetTenantInvitationsFunction(TenantInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [Function("GetTenantInvitations")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/invitations")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var invitations = await _invitationService.GetByTenantAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, invitations, cancellationToken: cancellationToken);
    }
}
