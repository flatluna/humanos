using HumanOS.Agents.Studio;
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
            .AsSplitQuery() // Nodes/Edges are sibling collections — avoids a cartesian join (see GetFullGraphAsync's comment).
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
            .AsSplitQuery() // Nodes/Edges are sibling collections — avoids a cartesian join (see GetFullGraphAsync's comment).
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

        /// <summary>0-based index of the source document chapter/module this
        /// node belongs to (see <see cref="Models.Capabilities.Graph.CapabilityGraphNode.ChapterIndex"/>) —
        /// what the "climb" preview now groups nodes by. Null for nodes
        /// whose chapter couldn't be traced, or capabilities created before
        /// this field existed — the client falls back to edge-tier grouping
        /// in that case.</summary>
        public int? ChapterIndex { get; set; }

        /// <summary>Best passed assessment score (0-100) of this person's MOST RECENT
        /// completed attempt at this node — same semantics as NodeSummaryResult.FinalScore.
        /// Null while the node isn't Mastered yet.</summary>
        public int? FinalScore { get; set; }

        /// <summary>When this person MOST RECENTLY completed this node (max CompletedDate
        /// across all their Completed LearningSessionNode rows for it) — same semantics as
        /// NodeSummaryResult.LastCompletedDate, just computed for every completed node in the
        /// Capability at once so bulk "recency" views (e.g. the Brain recall grid) don't need
        /// a separate GetNodeSummary call per node. Null while the node isn't Mastered yet.</summary>
        public DateTime? LastCompletedDate { get; set; }

        /// <summary>How many times this person has genuinely mastered this node (distinct
        /// Completed LearningSessionNode rows with a Passed AssessmentResult) — the "review
        /// count" that drives the client-side forgetting-curve (retrievability) calculation
        /// on the Brain recall grid. 0 while the node isn't Mastered yet.</summary>
        public int ReviewCount { get; set; }

        /// <summary>True once this person has ever COMPLETED this node's optional Deep Dive
        /// step (mastered every example card in order — see DeepDiveLoopGate) on their MOST
        /// RECENT LearningSessionNode attempt at this node — independent of whether the whole
        /// node itself is Mastered yet (Deep Dive sits between Teaching and Recall, well before
        /// Assessment decides the node's overall state). Drives the "big pretty star" shown on
        /// the node's map bubble/card (product decision, 2026-07-29). Always false for nodes
        /// whose blueprint has no Deep Dive step.</summary>
        public bool DeepDiveMastered { get; set; }
    }

    public sealed class GraphMapEdgeInfo
    {
        public Guid SourceNodeId { get; set; }
        public Guid TargetNodeId { get; set; }
        public RelationshipType RelationshipType { get; set; }
    }

    /// <summary>Professor-given name for one computed "climb level"/tier —
    /// see <see cref="Models.Capabilities.Graph.CapabilityGraphLevel"/>.</summary>
    public sealed class GraphLevelInfo
    {
        public int LevelIndex { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class FullGraphResult
    {
        public Guid CapabilityGraphId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<GraphMapNodeInfo> Nodes { get; set; } = new();
        public List<GraphMapEdgeInfo> Edges { get; set; } = new();

        /// <summary>Professor-given names for computed graph levels, keyed by
        /// LevelIndex — only present for levels the professor has explicitly
        /// named. The client falls back to a generic "Level N" label for any
        /// level with no entry here.</summary>
        public List<GraphLevelInfo> Levels { get; set; } = new();

        /// <summary>Document-wide executive summary generated by
        /// <see cref="DocumentContextAgent"/> (2026-07-20). Null for
        /// capabilities created before that agent existed, or when the
        /// best-effort extraction failed/wasn't configured for that run.</summary>
        public string? ExecutiveSummary { get; set; }

        /// <summary>Named entities explicitly grounded in the source
        /// material (see <see cref="DocumentEntityDto"/>). Empty when none
        /// were found or the field is null/unset.</summary>
        public List<DocumentEntityDto> KeyEntities { get; set; } = new();

        /// <summary>Student-facing topical axis this Capability belongs to
        /// (Matemáticas, Idiomas, Cocina...) — <see cref="Models.Capabilities.Subject"/>,
        /// NOT <see cref="Models.Capabilities.CapabilityDomain"/> (which is
        /// Studio-only authoring metadata never shown to students). Null
        /// when the Capability hasn't been categorized yet. Used e.g. by
        /// the Brain recall grid to group/filter by subject.</summary>
        public string? SubjectCode { get; set; }
        public string? SubjectName { get; set; }

        /// <summary>"job-skills" | "soft-skills" | "department-processes" |
        /// "hr-processes" | null — see <see cref="Models.Capabilities.Capability.Category"/>.
        /// Powers the "tipo de curso" filter on the Brain recall grid
        /// (2026-08-02).</summary>
        public string? Category { get; set; }
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
        string languageCode = "en",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var normalizedLanguageCode = languageCode.Trim();

        // AsSplitQuery() is required here: Nodes.Illustrations, Edges, Levels and
        // Capability.Subject.Translations are sibling collections off the same
        // CapabilityGraph root. Without it, EF Core's default single-query behavior
        // joins all of them together in one SQL statement, producing a cartesian
        // product (illustration rows × edge rows × level rows) that grows
        // multiplicatively — not linearly — with graph size. Measured impact: a
        // 20-node graph (139 illustrations × 19 edges ≈ 2,641 joined rows) took
        // ~10.4s vs a 12-node graph (77 × 12 ≈ 924 rows) at ~3s. AsSplitQuery
        // issues one SQL query per collection instead.
        var graph = await dbContext.CapabilityGraphs
            .AsNoTracking()
            .AsSplitQuery()
            .Include(g => g.Nodes).ThenInclude(n => n.Illustrations)
            .Include(g => g.Edges)
            .Include(g => g.Levels)
            .Include(g => g.Capability).ThenInclude(c => c!.Subject).ThenInclude(s => s!.Translations)
            .FirstOrDefaultAsync(g => g.CapabilityId == capabilityId, cancellationToken);

        if (graph is null)
        {
            return null;
        }

        var completedNodeIds = await GetCompletedNodeIdsAsync(dbContext, personId, capabilityId, cancellationToken);
        var nodeScores = await GetNodeScoresAsync(dbContext, personId, capabilityId, cancellationToken);
        var nodeLastCompletedDates = await GetNodeLastCompletedDatesAsync(dbContext, personId, capabilityId, cancellationToken);
        var nodeReviewCounts = await GetNodeReviewCountsAsync(dbContext, personId, capabilityId, cancellationToken);
        var deepDiveMasteredNodeIds = await GetDeepDiveMasteredNodeIdsAsync(dbContext, personId, capabilityId, cancellationToken);

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
                IllustrationId = node.Illustrations.FirstOrDefault()?.CapabilityGraphNodeIllustrationId,
                FinalScore = nodeScores.TryGetValue(node.CapabilityGraphNodeId, out var score) ? score : null,
                LastCompletedDate = nodeLastCompletedDates.TryGetValue(node.CapabilityGraphNodeId, out var lastCompleted) ? lastCompleted : null,
                ReviewCount = nodeReviewCounts.TryGetValue(node.CapabilityGraphNodeId, out var reviewCount) ? reviewCount : 0,
                DeepDiveMastered = deepDiveMasteredNodeIds.Contains(node.CapabilityGraphNodeId),
                ChapterIndex = node.ChapterIndex
            });
        }

        var edges = graph.Edges.Select(e => new GraphMapEdgeInfo
        {
            SourceNodeId = e.SourceNodeId,
            TargetNodeId = e.TargetNodeId,
            RelationshipType = e.RelationshipType
        }).ToList();

        List<DocumentEntityDto> keyEntities = new();
        if (!string.IsNullOrWhiteSpace(graph.KeyEntitiesJson))
        {
            try
            {
                keyEntities = System.Text.Json.JsonSerializer.Deserialize<List<DocumentEntityDto>>(graph.KeyEntitiesJson) ?? new();
            }
            catch (System.Text.Json.JsonException)
            {
                // Malformed/legacy data — degrade to an empty list rather than fail the whole graph request.
            }
        }

        var levels = graph.Levels
            .Select(l => new GraphLevelInfo { LevelIndex = l.LevelIndex, Name = l.Name })
            .ToList();

        return new FullGraphResult
        {
            CapabilityGraphId = graph.CapabilityGraphId,
            Name = graph.Name,
            Description = graph.Description,
            Nodes = nodes.OrderBy(n => n.SortOrder).ToList(),
            Edges = edges,
            Levels = levels,
            ExecutiveSummary = graph.ExecutiveSummary,
            KeyEntities = keyEntities,
            SubjectCode = graph.Capability?.Subject?.Code,
            SubjectName = graph.Capability?.Subject?.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Name
                ?? graph.Capability?.Subject?.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? graph.Capability?.Subject?.Name,
            Category = graph.Capability?.Category
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
    /// <summary>Every node this person has EVER completed the optional Deep Dive step for
    /// (any attempt, not just the most recent) \u2014 backs <see cref="GraphMapNodeInfo.DeepDiveMastered"/>.
    /// Simple "ever achieved" semantics (unlike FinalScore's "latest attempt" scoping) since
    /// this is a one-time achievement star, not a re-computed grade.</summary>
    private static async Task<HashSet<Guid>> GetDeepDiveMasteredNodeIdsAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var ids = await dbContext.LearningSessionSteps
            .AsNoTracking()
            .Where(s => s.StepType == ExperienceStepType.DeepDive
                     && s.Status == LearningSessionStepStatus.Completed
                     && s.LearningSessionNode!.LearningSession!.PersonId == personId
                     && s.LearningSessionNode!.LearningSession!.CapabilityId == capabilityId)
            .Select(s => s.LearningSessionNode!.CapabilityGraphNodeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    private static async Task<HashSet<Guid>> GetCompletedNodeIdsAsync(        HumanOsDbContext dbContext,
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

    /// <summary>Per-node best-passed assessment score (0-100) of this person's MOST RECENT
    /// completed attempt at each node — same "latest attempt's best passed score" semantics
    /// as <see cref="InstructorRuntimeOrchestrator.GetNodeSummaryAsync"/>'s FinalScore, just
    /// computed for every completed node in the Capability at once (for the map view).</summary>
    private static async Task<Dictionary<Guid, int>> GetNodeScoresAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var completedNodes = await dbContext.LearningSessionNodes
            .AsNoTracking()
            .Include(n => n.AssessmentResults)
            .Where(n => n.LearningSession!.PersonId == personId
                     && n.LearningSession!.CapabilityId == capabilityId
                     && n.Status == LearningSessionNodeStatus.Completed)
            .ToListAsync(cancellationToken);

        var scores = new Dictionary<Guid, int>();
        foreach (var group in completedNodes.GroupBy(n => n.CapabilityGraphNodeId))
        {
            var latest = group.OrderByDescending(n => n.CompletedDate).First();
            var bestPassed = latest.AssessmentResults
                .Where(a => a.Passed)
                .OrderByDescending(a => a.CreatedDate)
                .FirstOrDefault();
            if (bestPassed is not null)
            {
                scores[group.Key] = bestPassed.Score;
            }
        }

        return scores;
    }

    /// <summary>Per-node MOST RECENT completion date (max CompletedDate across all Completed
    /// LearningSessionNode rows) for this person in this Capability — the raw signal the
    /// Brain recall grid uses to show "hace cuánto lo repasaste" recency per node, without a
    /// separate GetNodeSummary call per node.</summary>
    private static async Task<Dictionary<Guid, DateTime>> GetNodeLastCompletedDatesAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var completedDates = await dbContext.LearningSessionNodes
            .AsNoTracking()
            .Where(n => n.LearningSession!.PersonId == personId
                     && n.LearningSession!.CapabilityId == capabilityId
                     && n.Status == LearningSessionNodeStatus.Completed
                     && n.CompletedDate != null)
            .Select(n => new { n.CapabilityGraphNodeId, n.CompletedDate })
            .ToListAsync(cancellationToken);

        var lastCompleted = new Dictionary<Guid, DateTime>();
        foreach (var group in completedDates.GroupBy(n => n.CapabilityGraphNodeId))
        {
            lastCompleted[group.Key] = group.Max(n => n.CompletedDate!.Value);
        }

        return lastCompleted;
    }

    /// <summary>Per-node count of distinct times this person has genuinely mastered a node
    /// (Completed LearningSessionNode rows that have a Passed AssessmentResult) — feeds the
    /// client-side forgetting-curve "stability" calculation (more reviews = a memory that
    /// decays more slowly), mirroring how spaced-repetition systems like Anki/FSRS grow the
    /// review interval after each successful recall.</summary>
    private static async Task<Dictionary<Guid, int>> GetNodeReviewCountsAsync(
        HumanOsDbContext dbContext,
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var completedNodes = await dbContext.LearningSessionNodes
            .AsNoTracking()
            .Include(n => n.AssessmentResults)
            .Where(n => n.LearningSession!.PersonId == personId
                     && n.LearningSession!.CapabilityId == capabilityId
                     && n.Status == LearningSessionNodeStatus.Completed)
            .ToListAsync(cancellationToken);

        var reviewCounts = new Dictionary<Guid, int>();
        foreach (var group in completedNodes.GroupBy(n => n.CapabilityGraphNodeId))
        {
            reviewCounts[group.Key] = group.Count(n => n.AssessmentResults.Any(a => a.Passed));
        }

        return reviewCounts;
    }
}
