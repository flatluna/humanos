using System.Net;
using HumanOS.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// TEMPORARY diagnostic endpoint (2026-07-15) — dumps the raw checkpoint
/// chain for a Runtime session to debug an observed checkpoint-tip
/// resolution bug in <c>RuntimeApiEngine.GetLatestCheckpointAsync</c>.
/// DELETE after the Paso 9 checkpoint-resume bug is fixed.
/// </summary>
public sealed class DebugRuntimeCheckpointChainFunction
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public DebugRuntimeCheckpointChainFunction(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [Function("DebugRuntimeCheckpointChain")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "debug/runtime-checkpoints/{runtimeSessionId:guid}")]
        HttpRequestData request,
        Guid runtimeSessionId,
        CancellationToken cancellationToken)
    {
        var engineSessionId = runtimeSessionId.ToString("N");

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await db.RuntimeWorkflowCheckpoints
            .AsNoTracking()
            .Where(x => x.SessionId == engineSessionId)
            .OrderBy(x => x.CreatedDate)
            .Select(x => new
            {
                x.RuntimeWorkflowCheckpointId,
                x.CheckpointId,
                x.ParentCheckpointId,
                x.CreatedDate,
                PayloadPreview = x.PayloadJson.Length > 200 ? x.PayloadJson.Substring(0, 200) : x.PayloadJson
            })
            .ToListAsync(cancellationToken);

        var status = await db.RuntimeSessionStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SessionId == engineSessionId, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new
        {
            EngineSessionId = engineSessionId,
            Count = rows.Count,
            Status = status,
            Rows = rows
        }, cancellationToken: cancellationToken);
    }

    [Function("DebugRuntimeRecentSessions")]
    public async Task<HttpResponseData> ListRecentAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "debug/runtime-recent-sessions")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var recent = await db.RuntimeWorkflowCheckpoints
            .AsNoTracking()
            .GroupBy(x => x.SessionId)
            .Select(g => new
            {
                SessionId = g.Key,
                Count = g.Count(),
                LastCreatedDate = g.Max(x => x.CreatedDate)
            })
            .OrderByDescending(x => x.LastCreatedDate)
            .Take(5)
            .ToListAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, recent, cancellationToken: cancellationToken);
    }
}
