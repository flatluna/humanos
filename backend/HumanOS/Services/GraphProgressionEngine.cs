using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Runtime Paso 5 — "Graph Progression Engine". Activates the CapabilityGraph
/// (previously a passive artifact from Studio) as the engine that decides
/// WHEN a student is allowed to advance to the next node.
///
/// Works identically for ANY Capability domain (math, languages, cooking,
/// medicine, soft skills, etc.) — this engine never references specific node
/// names or subject matter. It only reasons over generic graph structure
/// (<see cref="CapabilityGraphEdge"/>) plus the student's own persisted
/// completion history. Nothing here is hardcoded to any one capability.
///
/// VERSION 1 scope: only the "Requires" <see cref="RelationshipType"/> drives
/// unlocking. BuildsOn/Supports/RelatedTo edges may exist in the model but
/// are deliberately ignored for progression purposes in this version.
///
/// Source of truth — NEVER a duplicated/denormalized status:
/// a CapabilityGraphNode counts as "completed" for a person on a Capability
/// when there exists a <see cref="LearningSessionNode"/> (Status=Completed)
/// belonging to one of that person's LearningSessions on that Capability,
/// AND that LearningSessionNode has at least one
/// <see cref="LearningAssessmentResult"/> with Passed=true. Checking BOTH
/// signals (not just Status) guards against a node ever being treated as
/// "completed" for unlocking purposes without a genuine passing assessment
/// behind it — it costs nothing extra (single query) and matches the spec's
/// explicit instruction to determine progression using
/// CapabilityGraphEdge + LearningSessionNode.Status + LearningAssessmentResult.
/// </summary>
public sealed class GraphProgressionEngine
{
    /// <summary>Result of Método 1 — CanStartNodeAsync.</summary>
    public sealed class CanStartNodeResult
    {
        public bool CanStart { get; set; }
        public List<string> BlockedReasons { get; set; } = new();
    }

    /// <summary>A node reference used by GetAvailableNodesAsync/GetNewlyUnlockedNodesAsync — generic, subject-agnostic.</summary>
    public sealed class GraphNodeInfo
    {
        public Guid CapabilityGraphNodeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// Método 1 — ¿puede esta persona iniciar este nodo ahora mismo? A node
    /// with no Requires prerequisites is always startable (e.g. a root
    /// concept of the graph).
    /// </summary>
    public async Task<CanStartNodeResult> CanStartNodeAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        Guid capabilityId,
        Guid capabilityGraphNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var prerequisites = await GetPrerequisitesAsync(dbContext, capabilityGraphNodeId, cancellationToken);
        if (prerequisites.Count == 0)
        {
            return new CanStartNodeResult { CanStart = true };
        }

        var completedNodeIds = await GetCompletedNodeIdsAsync(dbContext, personId, capabilityId, cancellationToken);

        var result = new CanStartNodeResult { CanStart = true };
        foreach (var prerequisite in prerequisites)
        {
            if (!completedNodeIds.Contains(prerequisite.CapabilityGraphNodeId))
            {
                result.CanStart = false;
                result.BlockedReasons.Add($"{prerequisite.Name} todavía no está completado.");
            }
        }

        return result;
    }

    /// <summary>
    /// Método 2 — de todos los nodos del grafo de esta Capability que la
    /// persona AÚN no ha completado, ¿cuáles puede iniciar ahora mismo?
    /// </summary>
    public async Task<List<GraphNodeInfo>> GetAvailableNodesAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var graph = await dbContext.CapabilityGraphs
            .AsNoTracking()
            .Include(g => g.Nodes)
            .Include(g => g.Edges)
            .FirstOrDefaultAsync(g => g.CapabilityId == capabilityId, cancellationToken);

        if (graph is null)
        {
            return new List<GraphNodeInfo>();
        }

        var completedNodeIds = await GetCompletedNodeIdsAsync(dbContext, personId, capabilityId, cancellationToken);

        var available = new List<GraphNodeInfo>();
        foreach (var node in graph.Nodes)
        {
            if (completedNodeIds.Contains(node.CapabilityGraphNodeId))
            {
                continue;
            }

            if (IsUnlocked(node.CapabilityGraphNodeId, graph.Edges, completedNodeIds))
            {
                available.Add(new GraphNodeInfo
                {
                    CapabilityGraphNodeId = node.CapabilityGraphNodeId,
                    Name = node.Name,
                    SortOrder = node.SortOrder
                });
            }
        }

        return available.OrderBy(n => n.SortOrder).ToList();
    }

    /// <summary>Método 3 — ¿por qué está bloqueado este nodo específico? (CapabilityId se resuelve internamente a partir del propio nodo).</summary>
    public async Task<List<string>> GetBlockedReasonsAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        Guid capabilityGraphNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var node = await dbContext.CapabilityGraphNodes
            .AsNoTracking()
            .Include(n => n.CapabilityGraph)
            .FirstOrDefaultAsync(n => n.CapabilityGraphNodeId == capabilityGraphNodeId, cancellationToken);

        if (node?.CapabilityGraph is null)
        {
            throw new InvalidOperationException($"CapabilityGraphNode {capabilityGraphNodeId} not found.");
        }

        var result = await CanStartNodeAsync(
            dbContext, personId, node.CapabilityGraph.CapabilityId, capabilityGraphNodeId, cancellationToken);

        return result.BlockedReasons;
    }

    /// <summary>
    /// Método 4 — justo después de completar un nodo, ¿qué otros nodos
    /// quedaron disponibles como consecuencia directa? Sólo considera nodos
    /// que dependen DIRECTAMENTE (Requires) del nodo recién completado — la
    /// finalización de un nodo no puede afectar el estado de desbloqueo de
    /// ningún otro nodo del grafo.
    /// </summary>
    public async Task<List<GraphNodeInfo>> GetNewlyUnlockedNodesAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        Guid capabilityId,
        Guid completedCapabilityGraphNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var graph = await dbContext.CapabilityGraphs
            .AsNoTracking()
            .Include(g => g.Nodes)
            .Include(g => g.Edges)
            .FirstOrDefaultAsync(g => g.CapabilityId == capabilityId, cancellationToken);

        if (graph is null)
        {
            return new List<GraphNodeInfo>();
        }

        var directDependentIds = graph.Edges
            .Where(e => e.TargetNodeId == completedCapabilityGraphNodeId && e.RelationshipType == RelationshipType.Requires)
            .Select(e => e.SourceNodeId)
            .ToHashSet();

        if (directDependentIds.Count == 0)
        {
            return new List<GraphNodeInfo>();
        }

        var completedNodeIds = await GetCompletedNodeIdsAsync(dbContext, personId, capabilityId, cancellationToken);

        var newlyUnlocked = new List<GraphNodeInfo>();
        foreach (var node in graph.Nodes.Where(n => directDependentIds.Contains(n.CapabilityGraphNodeId)))
        {
            if (completedNodeIds.Contains(node.CapabilityGraphNodeId))
            {
                continue;
            }

            if (IsUnlocked(node.CapabilityGraphNodeId, graph.Edges, completedNodeIds))
            {
                newlyUnlocked.Add(new GraphNodeInfo
                {
                    CapabilityGraphNodeId = node.CapabilityGraphNodeId,
                    Name = node.Name,
                    SortOrder = node.SortOrder
                });
            }
        }

        return newlyUnlocked.OrderBy(n => n.SortOrder).ToList();
    }

    /// <summary>A node's full state for the map view: Locked / Available / Mastered. "NeedsReview" (MasteryStrength decay) is a V2 concept — not computed yet (see /memories/repo/student-graph-ui-redesign-final-design.md).</summary>
    public sealed class GraphMapNodeInfo
    {
        public Guid CapabilityGraphNodeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public string State { get; set; } = "Locked";
        public Guid? IllustrationId { get; set; }
    }

    public sealed class GraphMapEdgeInfo
    {
        public Guid SourceNodeId { get; set; }
        public Guid TargetNodeId { get; set; }
        public RelationshipType RelationshipType { get; set; }
    }

    public sealed class FullGraphResult
    {
        public Guid CapabilityGraphId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<GraphMapNodeInfo> Nodes { get; set; } = new();
        public List<GraphMapEdgeInfo> Edges { get; set; } = new();
    }

    /// <summary>
    /// Método 5 — el grafo COMPLETO de una Capability (todos los nodos, todas
    /// las aristas), con el estado de cada nodo ya calculado para esta
    /// persona (Locked/Available/Mastered). Pensado para alimentar el
    /// Capability Graph Map del alumno de un solo request.
    /// </summary>
    public async Task<FullGraphResult?> GetFullGraphAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var graph = await dbContext.CapabilityGraphs
            .AsNoTracking()
            .Include(g => g.Nodes).ThenInclude(n => n.Illustrations)
            .Include(g => g.Edges)
            .FirstOrDefaultAsync(g => g.CapabilityId == capabilityId, cancellationToken);

        if (graph is null)
        {
            return null;
        }

        var completedNodeIds = await GetCompletedNodeIdsAsync(dbContext, personId, capabilityId, cancellationToken);

        var nodes = new List<GraphMapNodeInfo>();
        foreach (var node in graph.Nodes)
        {
            string state;
            if (completedNodeIds.Contains(node.CapabilityGraphNodeId))
            {
                state = "Mastered";
            }
            else if (IsUnlocked(node.CapabilityGraphNodeId, graph.Edges, completedNodeIds))
            {
                state = "Available";
            }
            else
            {
                state = "Locked";
            }

            nodes.Add(new GraphMapNodeInfo
            {
                CapabilityGraphNodeId = node.CapabilityGraphNodeId,
                Name = node.Name,
                Description = node.Description,
                SortOrder = node.SortOrder,
                State = state,
                IllustrationId = node.Illustrations.FirstOrDefault()?.CapabilityGraphNodeIllustrationId
            });
        }

        var edges = graph.Edges.Select(e => new GraphMapEdgeInfo
        {
            SourceNodeId = e.SourceNodeId,
            TargetNodeId = e.TargetNodeId,
            RelationshipType = e.RelationshipType
        }).ToList();

        return new FullGraphResult
        {
            CapabilityGraphId = graph.CapabilityGraphId,
            Name = graph.Name,
            Description = graph.Description,
            Nodes = nodes.OrderBy(n => n.SortOrder).ToList(),
            Edges = edges
        };
    }

    /// <summary>Shared rule (Criterio de desbloqueo): true only if ALL Requires-prerequisites of this node are in the completed set.</summary>
    private static bool IsUnlocked(Guid nodeId, IEnumerable<CapabilityGraphEdge> edges, HashSet<Guid> completedNodeIds)
    {
        var requiredNodeIds = edges
            .Where(e => e.SourceNodeId == nodeId && e.RelationshipType == RelationshipType.Requires)
            .Select(e => e.TargetNodeId);

        return requiredNodeIds.All(completedNodeIds.Contains);
    }

    /// <summary>Prerequisite nodes (Requires-only) of a given node, with their names for BlockedReasons messages.</summary>
    private static async Task<List<GraphNodeInfo>> GetPrerequisitesAsync(
        HumanOsDbContext dbContext,
        Guid capabilityGraphNodeId,
        CancellationToken cancellationToken)
    {
        var prerequisiteIds = await dbContext.CapabilityGraphEdges
            .AsNoTracking()
            .Where(e => e.SourceNodeId == capabilityGraphNodeId && e.RelationshipType == RelationshipType.Requires)
            .Select(e => e.TargetNodeId)
            .ToListAsync(cancellationToken);

        if (prerequisiteIds.Count == 0)
        {
            return new List<GraphNodeInfo>();
        }

        return await dbContext.CapabilityGraphNodes
            .AsNoTracking()
            .Where(n => prerequisiteIds.Contains(n.CapabilityGraphNodeId))
            .Select(n => new GraphNodeInfo
            {
                CapabilityGraphNodeId = n.CapabilityGraphNodeId,
                Name = n.Name,
                SortOrder = n.SortOrder
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>The set of CapabilityGraphNodeIds this person has genuinely completed (Status=Completed AND a Passed assessment) for this Capability.</summary>
    private static async Task<HashSet<Guid>> GetCompletedNodeIdsAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var ids = await dbContext.LearningSessionNodes
            .AsNoTracking()
            .Where(n => n.LearningSession!.PersonId == personId
                     && n.LearningSession!.CapabilityId == capabilityId
                     && n.Status == LearningSessionNodeStatus.Completed
                     && n.AssessmentResults.Any(a => a.Passed))
            .Select(n => n.CapabilityGraphNodeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }
}
