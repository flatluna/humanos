using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>POST /tenants/{tenantId}/skills/sync-from-roles — one-time/
/// on-demand backfill that reconciles every active Job Role's free-text
/// skill lists into the shared catalog (see
/// SkillService.SyncCatalogForTenantAsync). Needed for roles that
/// existed before the catalog feature shipped and were never re-saved
/// since. Idempotent — safe to call again any time.</summary>
public sealed class SyncSkillsFromJobRolesFunction
{
    private readonly SkillService _skillService;

    public SyncSkillsFromJobRolesFunction(SkillService skillService)
    {
        _skillService = skillService;
    }

    [Function("SyncSkillsFromJobRoles")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tenants/{tenantId:guid}/skills/sync-from-roles")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var syncedCount = await _skillService.SyncCatalogForTenantAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(
            request, new { syncedJobRoleCount = syncedCount }, cancellationToken: cancellationToken);
    }
}
