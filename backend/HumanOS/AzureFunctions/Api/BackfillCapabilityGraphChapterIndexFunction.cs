using System.Net;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// One-off/on-demand backfill for CapabilityGraphs whose levels ended up
/// duplicated/broken, in TWO possible ways:
/// 1. Created before <see cref="Models.Capabilities.Graph.CapabilityGraphNode.ChapterIndex"/>
///    existed (null ChapterIndex on every node) — nodes were still grouped
///    in the student preview UI by prerequisite-edge DEPTH, which could put
///    two sibling nodes from the SAME source chapter into different rows.
/// 2. Created by an earlier, buggy build of <see cref="Services.CapabilityGraphLevelAutoNamer"/>'s
///    pipeline wiring (fixed 2026-08-04 in PdfCapabilityGraphPipelineService)
///    that assigned a DISTINCT chapter number per source-document SUBSECTION
///    instead of collapsing subsections sharing the same parent module —
///    ChapterIndex IS already set per node, but several consecutive
///    ChapterIndex/LevelIndex values share the exact same Level Name (e.g.
///    3 levels all named 'Introduction' instead of 1).
/// Both cases produce the same symptom (the "duplicated level" bug) and are
/// fixed the same way here: resolve each node's CURRENT level name (from its
/// own ChapterIndex when set, falling back to the old edge-depth tier only
/// when ChapterIndex is null), then regroup nodes by that NAME in document
/// order — assigning a fresh, de-duplicated, sequential ChapterIndex/
/// LevelIndex per distinct name — and replace the graph's Levels rows to
/// match. Idempotent: safe to call again (recomputes from scratch every
/// time).
/// </summary>
public sealed class BackfillCapabilityGraphChapterIndexFunction
{
    private readonly HumanOsDbContext _dbContext;

    public BackfillCapabilityGraphChapterIndexFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Function("BackfillCapabilityGraphChapterIndex")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "capabilities/{capabilityId:guid}/graph/backfill-chapter-index")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var graph = await _dbContext.CapabilityGraphs
            .Include(g => g.Nodes)
            .Include(g => g.Edges)
            .Include(g => g.Levels)
            .SingleOrDefaultAsync(g => g.CapabilityId == capabilityId, cancellationToken);

        if (graph is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "GraphNotFound",
                $"No CapabilityGraph found for capability {capabilityId}.", cancellationToken);
        }

        // ---- 1. Recompute each node's OLD tier — identical algorithm to
        // the one PreviewGraphPage.tsx's groupIntoLevels/groupByPrerequisiteTier
        // still falls back to: a node's tier is 1 + the deepest tier among
        // its prerequisites (nodes with no prerequisites sit at tier 0). Only
        // used as a fallback below, for nodes whose ChapterIndex is null. ----
        var prerequisitesOf = graph.Nodes.ToDictionary(n => n.CapabilityGraphNodeId, _ => new List<Guid>());
        foreach (var edge in graph.Edges)
        {
            if (prerequisitesOf.TryGetValue(edge.SourceNodeId, out var prereqs))
            {
                prereqs.Add(edge.TargetNodeId);
            }
        }

        var tierByNodeId = new Dictionary<Guid, int>();
        int TierOf(Guid nodeId, HashSet<Guid> visiting)
        {
            if (tierByNodeId.TryGetValue(nodeId, out var cached)) return cached;
            if (!visiting.Add(nodeId)) return 0;
            var prereqs = prerequisitesOf.TryGetValue(nodeId, out var list) ? list : [];
            var tier = prereqs.Count == 0 ? 0 : 1 + prereqs.Max(p => TierOf(p, visiting));
            visiting.Remove(nodeId);
            tierByNodeId[nodeId] = tier;
            return tier;
        }
        foreach (var node in graph.Nodes)
        {
            TierOf(node.CapabilityGraphNodeId, []);
        }

        // ---- 2. Resolve each node's CURRENT level name: prefer its own
        // already-set ChapterIndex/LevelIndex (handles the "duplicate level
        // name per subsection" bug, where ChapterIndex is set but several
        // distinct LevelIndex values share one Name) — only fall back to the
        // OLD edge-depth tier for nodes whose ChapterIndex was never set
        // (graphs created before that field existed). ----
        var nameByLevelIndex = graph.Levels.ToDictionary(l => l.LevelIndex, l => l.Name);

        string? NameOf(CapabilityGraphNode node)
        {
            if (node.ChapterIndex is int chapterIndex && nameByLevelIndex.TryGetValue(chapterIndex, out var nameFromChapterIndex))
            {
                return nameFromChapterIndex;
            }
            return tierByNodeId.TryGetValue(node.CapabilityGraphNodeId, out var tier)
                && nameByLevelIndex.TryGetValue(tier, out var nameFromTier)
                ? nameFromTier
                : null;
        }

        // ---- 3. Group nodes by NAME in document order (SortOrder),
        // assigning a fresh de-duplicated sequential ChapterIndex per
        // distinct name. ----
        var orderedNodes = graph.Nodes.OrderBy(n => n.SortOrder).ToList();
        var nameOrder = new List<string>();
        foreach (var node in orderedNodes)
        {
            var name = NameOf(node);
            if (name is not null && !nameOrder.Contains(name))
            {
                nameOrder.Add(name);
            }
        }

        var chapterIndexByName = nameOrder
            .Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index);

        foreach (var node in orderedNodes)
        {
            var name = NameOf(node);
            node.ChapterIndex = name is not null ? chapterIndexByName[name] : null;
        }

        // ---- 4. Replace the graph's Levels rows to match the new,
        // de-duplicated ChapterIndex numbering. ----
        _dbContext.CapabilityGraphLevels.RemoveRange(graph.Levels);
        foreach (var name in nameOrder)
        {
            _dbContext.CapabilityGraphLevels.Add(new CapabilityGraphLevel
            {
                CapabilityGraphId = graph.CapabilityGraphId,
                LevelIndex = chapterIndexByName[name],
                Name = name,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new
            {
                CapabilityGraphId = graph.CapabilityGraphId,
                NodesUpdated = orderedNodes.Count,
                ChaptersFound = nameOrder.Count,
                ChapterNames = nameOrder
            },
            cancellationToken: cancellationToken);
    }
}
