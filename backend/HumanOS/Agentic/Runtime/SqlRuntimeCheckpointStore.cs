using System.Text.Json;
using HumanOS.Data;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Azure SQL-backed implementation of the Agent Framework Workflows'
/// <see cref="ICheckpointStore{TStoreObject}"/> contract (fixed Paso 3,
/// 2026-07-14) — lets a paused Runtime session (waiting on learner
/// evidence, possibly for hours or days) survive a process restart. Backed
/// by the single, deliberately domain-free <see cref="RuntimeWorkflowCheckpoint"/>
/// table — this class treats <c>PayloadJson</c> as fully opaque; it never
/// inspects or interprets the Workflow engine's serialized state.
/// </summary>
/// <remarks>
/// Uses <see cref="IDbContextFactory{TContext}"/> (not a directly-injected
/// <see cref="HumanOsDbContext"/>) so this store is safe to register as a
/// Singleton — same captive-dependency-avoidance pattern already used by
/// Studio's <c>PublishExecutor</c>/<c>CapabilityCreationOrchestrator</c>.
/// </remarks>
internal sealed class SqlRuntimeCheckpointStore : ICheckpointStore<JsonElement>
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public SqlRuntimeCheckpointStore(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async ValueTask<CheckpointInfo> CreateCheckpointAsync(
        string sessionId,
        JsonElement value,
        CheckpointInfo? parentCheckpointInfo)
    {
        var checkpointInfo = new CheckpointInfo(sessionId, Guid.NewGuid().ToString("N"));

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        db.RuntimeWorkflowCheckpoints.Add(new RuntimeWorkflowCheckpoint
        {
            RuntimeWorkflowCheckpointId = Guid.NewGuid(),
            SessionId = sessionId,
            CheckpointId = checkpointInfo.CheckpointId,
            ParentCheckpointId = parentCheckpointInfo?.CheckpointId,
            PayloadJson = value.GetRawText(),
            CreatedDate = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        return checkpointInfo;
    }

    public async ValueTask<JsonElement> RetrieveCheckpointAsync(
        string sessionId,
        CheckpointInfo checkpointInfo)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var row = await db.RuntimeWorkflowCheckpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.SessionId == sessionId &&
                x.CheckpointId == checkpointInfo.CheckpointId);

        if (row is null)
        {
            throw new InvalidOperationException(
                $"No RuntimeWorkflowCheckpoint found for session '{sessionId}', checkpoint '{checkpointInfo.CheckpointId}'.");
        }

        using var document = JsonDocument.Parse(row.PayloadJson);
        return document.RootElement.Clone();
    }

    public async ValueTask<IEnumerable<CheckpointInfo>> RetrieveIndexAsync(
        string sessionId,
        CheckpointInfo? parentCheckpointInfo)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var query = db.RuntimeWorkflowCheckpoints
            .AsNoTracking()
            .Where(x => x.SessionId == sessionId);

        if (parentCheckpointInfo is not null)
        {
            query = query.Where(x => x.ParentCheckpointId == parentCheckpointInfo.CheckpointId);
        }

        var checkpointIds = await query
            .OrderBy(x => x.CreatedDate)
            .Select(x => x.CheckpointId)
            .ToListAsync();

        return checkpointIds.Select(id => new CheckpointInfo(sessionId, id));
    }
}
