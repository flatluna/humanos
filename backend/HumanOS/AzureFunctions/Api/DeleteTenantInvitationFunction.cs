using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class DeleteTenantInvitationFunction
{
    private readonly TenantInvitationService _invitationService;

    public DeleteTenantInvitationFunction(TenantInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [Function("DeleteTenantInvitation")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tenants/{tenantId:guid}/invitations/{tenantInvitationId:guid}")]
        HttpRequestData request,
        Guid tenantId,
        Guid tenantInvitationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _invitationService.DeletePendingAsync(tenantId, tenantInvitationId, cancellationToken);
            return request.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "InvitationNotFound",
                ex.Message,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidInvitationState",
                ex.Message,
                cancellationToken);
        }
    }
}
