using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Deletes an entire Video Production (2026-09-24) — the
/// VideoProduction row, all of its clips, and every blob (per-clip .mp4s
/// plus the merged final video). Irreversible, so the frontend must get
/// explicit confirmation from the reviewer before calling this.</summary>
public sealed class DeleteVideoProductionFunction
{
    private readonly VideoProductionOrchestrator _orchestrator;
    private readonly HumanOsDbContext _dbContext;

    public DeleteVideoProductionFunction(VideoProductionOrchestrator orchestrator, HumanOsDbContext dbContext)
    {
        _orchestrator = orchestrator;
        _dbContext = dbContext;
    }

    [Function("DeleteVideoProduction")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "studio/video-productions/{videoProductionId:guid}")]
        HttpRequestData request,
        Guid videoProductionId,
        CancellationToken cancellationToken)
    {
        // 2026-11-10 fix: previously anyone who knew/guessed a
        // VideoProductionId could delete it regardless of which tenant or
        // account created it. Now requires the caller's own tenant/oid
        // (when the row actually has an owner recorded) to match before
        // deleting — mirrors the same ownership check ListVideoProductions
        // now applies.
        var existing = await _dbContext.VideoProductions
            .AsNoTracking()
            .Where(p => p.VideoProductionId == videoProductionId)
            .Select(p => new { p.TenantId, p.OwnerOid })
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            var callerTenantId = request.Headers.TryGetValues("X-Studio-Tenant-Id", out var tenantValues) &&
                Guid.TryParse(tenantValues.FirstOrDefault(), out var parsedTenantId)
                    ? parsedTenantId
                    : (Guid?)null;
            var callerOid = request.Headers.TryGetValues("X-Azure-OID", out var oidValues)
                ? oidValues.FirstOrDefault()
                : null;

            var tenantMismatch = callerTenantId is not null && existing.TenantId != callerTenantId;
            var ownerMismatch = !string.IsNullOrWhiteSpace(existing.OwnerOid) && existing.OwnerOid != callerOid;
            if (tenantMismatch || ownerMismatch)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.Forbidden, "NotOwner", "You do not have permission to delete this video production.", cancellationToken);
            }
        }

        try
        {
            await _orchestrator.DeleteProductionAsync(videoProductionId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new { deleted = true }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ProductionNotFound", ex.Message, cancellationToken);
        }
    }
}
