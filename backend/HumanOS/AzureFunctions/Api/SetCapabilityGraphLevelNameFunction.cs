using System.Net;
using HumanOS.Contracts.Capabilities;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Capability Studio — professor/author tool: names one computed "climb
/// level" (a tier of the graph's prerequisite DAG — see
/// capabilitystudio/PreviewGraphPage.tsx's groupIntoLevels) of an
/// already-persisted CapabilityGraph, e.g. "Capítulo 1: Vocales" instead of
/// the generic "Nivel 1" fallback (2026-07-30).
///
/// Levels themselves are never a stored structural concept — only this
/// OPTIONAL display name, keyed by the level's current index, is persisted
/// (see <see cref="CapabilityGraphLevel"/>). If the graph's structure later
/// changes such that the index no longer lines up with the same nodes, the
/// name simply stops being shown for that index (falls back to "Nivel N")
/// rather than silently mislabeling a different set of nodes.
/// </summary>
public sealed class SetCapabilityGraphLevelNameFunction
{
    private readonly HumanOsDbContext _dbContext;

    public SetCapabilityGraphLevelNameFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("SetCapabilityGraphLevelName")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "capabilities/{capabilityId:guid}/graph/levels/{levelIndex:int}")]
        HttpRequestData request,
        Guid capabilityId,
        int levelIndex,
        CancellationToken cancellationToken)
    {
        if (levelIndex < 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidLevelIndex",
                "levelIndex must be zero or a positive integer.", cancellationToken);
        }

        var body = await request.ReadFromJsonAsync<SetCapabilityGraphLevelNameRequest>(cancellationToken)
            ?? new SetCapabilityGraphLevelNameRequest();

        var graph = await _dbContext.CapabilityGraphs
            .Include(g => g.Levels)
            .FirstOrDefaultAsync(g => g.CapabilityId == capabilityId, cancellationToken);

        if (graph is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "GraphNotFound",
                $"No CapabilityGraph found for capability {capabilityId}.", cancellationToken);
        }

        var existing = graph.Levels.FirstOrDefault(l => l.LevelIndex == levelIndex);
        var trimmedName = body.Name.Trim();

        if (string.IsNullOrEmpty(trimmedName))
        {
            // Empty name: delete any existing entry, reverting to the generic fallback label.
            if (existing is not null)
            {
                _dbContext.CapabilityGraphLevels.Remove(existing);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, new { LevelIndex = levelIndex, Name = (string?)null }, cancellationToken: cancellationToken);
        }

        if (existing is not null)
        {
            existing.Name = trimmedName;
            existing.UpdatedDate = DateTime.UtcNow;
        }
        else
        {
            _dbContext.CapabilityGraphLevels.Add(new CapabilityGraphLevel
            {
                CapabilityGraphId = graph.CapabilityGraphId,
                LevelIndex = levelIndex,
                Name = trimmedName,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request, new { LevelIndex = levelIndex, Name = trimmedName }, cancellationToken: cancellationToken);
    }
}
