using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Irreversibly deletes a Tenant and everything exclusively owned
/// by it (2026-10-01) — see <see cref="TenantDeletionService"/> doc comment
/// for the exact scope (global/shared content like Capability and
/// StructuredLearningDocument is NEVER touched). Requires the caller to
/// pass ?confirm={tenant's own Slug} as the "mega warning" gate; returns a
/// <see cref="TenantDeletionSummary"/> receipt of exactly what was
/// removed.</summary>
public sealed class DeleteTenantFunction
{
    private readonly TenantDeletionService _deletionService;

    public DeleteTenantFunction(TenantDeletionService deletionService)
    {
        _deletionService = deletionService;
    }

    [Function("DeleteTenant")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tenants/{tenantId:guid}")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var confirm = query["confirm"];

        if (string.IsNullOrWhiteSpace(confirm))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "ConfirmationRequired",
                "Pass ?confirm={tenant slug} to acknowledge this irreversible deletion.", cancellationToken);
        }

        try
        {
            var summary = await _deletionService.DeleteTenantAsync(tenantId, confirm, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, summary, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "DeleteTenantFailed", ex.Message, cancellationToken);
        }
    }
}
