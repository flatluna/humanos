using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// PASO 2 persistence layer: takes the structured output of GraphArchitectAgent
/// (a <see cref="CapabilityGraphResponse"/>, still in-memory) plus the metadata
/// of any illustrations already generated (gpt-image-1.5) and already uploaded
/// to Azure Data Lake, and persists everything as real rows in SQL:
///
///   CapabilityGraph
///   CapabilityGraphNode (with AcademicDefinition/Interpretation/Examples/Applications/References)
///   CapabilityGraphEdge
///   CapabilityGraphNodeIllustration (metadata only — StoragePath/Prompt/etc.)
///
/// Image bytes themselves are NEVER touched here — they already live in Data
/// Lake by the time this service runs; this only writes the SQL pointer rows.
/// </summary>
public sealed class CapabilityGraphPersistenceService
{
    /// <summary>Metadata for one already-generated-and-uploaded illustration, keyed by the
    /// GraphNodeDto.NodeId it belongs to (the same Guid used when resolving edges).</summary>
    public sealed class NodeIllustrationRecord
    {
        public Guid NodeId { get; set; }
        public string StoragePath { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string? Caption { get; set; }
        public string ImageModel { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public IllustrationPurpose Purpose { get; set; }
    }

    /// <summary>
    /// Persists a full CapabilityGraph (nodes + edges + illustration metadata) for the
    /// given Capability. Call <see cref="HumanOsDbContext.SaveChangesAsync(CancellationToken)"/>
    /// side-effect happens internally — this method commits the transaction itself.
    /// </summary>
    /// <param name="executiveSummary">Optional document-wide executive
    /// summary (2026-07-20 — see <see cref="Agents.Studio.DocumentContextAgent"/>),
    /// null when the extraction agent isn't configured or the call failed.</param>
    /// <param name="keyEntitiesJson">Optional JSON-serialized
    /// <see cref="Agents.Studio.DocumentEntityDto"/> list, same conditions as
    /// <paramref name="executiveSummary"/>.</param>
    /// <param name="coverImageStoragePath">Optional blob StoragePath of the
    /// course-level cover image (2026-07-21), null when generation wasn't
    /// configured or failed (best-effort).</param>
    /// <param name="brandLogoStoragePath">Optional blob StoragePath of the
    /// company/brand logo the designer attached at creation time (2026-08-04),
    /// null when no logo was attached.</param>
    /// <param name="chapterIndexByNodeId">Optional per-node source-chapter
    /// grouping (2026-08-04, keyed by <see cref="GraphNodeDto.NodeId"/>) —
    /// see <see cref="CapabilityGraphLevelAutoNamer"/> and
    /// <see cref="Models.Capabilities.Graph.CapabilityGraphNode.ChapterIndex"/>.
    /// Nodes absent from this map are persisted with a null ChapterIndex.</param>
    public async Task<CapabilityGraph> PersistAsync(
        HumanOsDbContext dbContext,
        Guid capabilityId,
        CapabilityGraphResponse graph,
        IReadOnlyList<NodeIllustrationRecord>? illustrations,
        CancellationToken cancellationToken = default,
        string? executiveSummary = null,
        string? keyEntitiesJson = null,
        string? coverImageStoragePath = null,
        string? brandLogoStoragePath = null,
        IReadOnlyDictionary<Guid, int>? chapterIndexByNodeId = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(graph);

        var graphEntity = new CapabilityGraph
        {
            CapabilityGraphId = graph.CapabilityGraphId != Guid.Empty ? graph.CapabilityGraphId : Guid.NewGuid(),
            CapabilityId = capabilityId,
            Name = graph.Name,
            Description = graph.Description,
            ExecutiveSummary = executiveSummary,
            KeyEntitiesJson = keyEntitiesJson,
            CoverImageStoragePath = coverImageStoragePath,
            BrandLogoStoragePath = brandLogoStoragePath,
            CreatedDate = DateTime.UtcNow
        };

        foreach (var node in graph.Nodes)
        {
            var nodeEntity = new CapabilityGraphNode
            {
                CapabilityGraphNodeId = node.NodeId != Guid.Empty ? node.NodeId : Guid.NewGuid(),
                CapabilityGraphId = graphEntity.CapabilityGraphId,
                Name = node.Name,
                Description = node.Description,
                // NodeType/RelationshipType numeric values are aligned 1:1
                // (Concept=0/Skill=1, Requires=0/BuildsOn=1) between the
                // agent DTO enums and the persistence enums by design.
                NodeType = (LearningNodeType)(int)node.NodeType,
                SortOrder = node.SortOrder,
                AcademicDefinition = node.AcademicDefinition,
                Interpretation = node.Interpretation,
                ExamplesJson = JsonSerializer.Serialize(node.Examples),
                ApplicationsJson = JsonSerializer.Serialize(node.Applications),
                ReferencesJson = JsonSerializer.Serialize(node.References),
                ChapterIndex = chapterIndexByNodeId is not null && chapterIndexByNodeId.TryGetValue(node.NodeId, out var chapterIndex) ? chapterIndex : null,
                CreatedDate = DateTime.UtcNow
            };

            graphEntity.Nodes.Add(nodeEntity);
        }

        foreach (var edge in graph.Edges)
        {
            // Edges whose Source/TargetNodeId couldn't be resolved to a real
            // node (LLM referenced an unknown node name) are skipped rather
            // than persisted with Guid.Empty, which would violate the FK.
            if (edge.SourceNodeId == Guid.Empty || edge.TargetNodeId == Guid.Empty)
            {
                continue;
            }

            graphEntity.Edges.Add(new CapabilityGraphEdge
            {
                CapabilityGraphEdgeId = edge.EdgeId != Guid.Empty ? edge.EdgeId : Guid.NewGuid(),
                CapabilityGraphId = graphEntity.CapabilityGraphId,
                SourceNodeId = edge.SourceNodeId,
                TargetNodeId = edge.TargetNodeId,
                RelationshipType = (RelationshipType)(int)edge.RelationshipType,
                CreatedDate = DateTime.UtcNow
            });
        }

        if (illustrations is not null)
        {
            foreach (var illustration in illustrations)
            {
                var nodeEntity = graphEntity.Nodes.FirstOrDefault(n => n.CapabilityGraphNodeId == illustration.NodeId);
                if (nodeEntity is null)
                {
                    continue;
                }

                nodeEntity.Illustrations.Add(new CapabilityGraphNodeIllustration
                {
                    CapabilityGraphNodeIllustrationId = Guid.NewGuid(),
                    CapabilityGraphNodeId = nodeEntity.CapabilityGraphNodeId,
                    StoragePath = illustration.StoragePath,
                    Prompt = illustration.Prompt,
                    Caption = illustration.Caption,
                    ImageModel = illustration.ImageModel,
                    Width = illustration.Width,
                    Height = illustration.Height,
                    Purpose = illustration.Purpose,
                    CreatedDate = DateTime.UtcNow
                });
            }
        }

        dbContext.CapabilityGraphs.Add(graphEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return graphEntity;
    }

    /// <summary>
    /// Studio "professor" tool — inserts ONE new node (plus its Requires
    /// edges to/from existing nodes, plus optional illustration metadata)
    /// into an ALREADY-PERSISTED CapabilityGraph. Unlike <see cref="PersistAsync"/>
    /// (which creates a whole new graph), this never touches any other node
    /// or edge already in the graph.
    /// </summary>
    /// <param name="requiresNodeIds">Existing node ids the new node REQUIRES
    /// (edges created as NewNode --Requires--> existing).</param>
    /// <param name="requiredByNodeIds">Existing node ids that will now
    /// REQUIRE the new node (edges created as existing --Requires--> NewNode).</param>
    /// <param name="sortOrder">Pre-computed SortOrder for the new node
    /// (caller decides — typically max(existing SortOrder) + 1).</param>
    public async Task<CapabilityGraphNode> AddNodeAsync(
        HumanOsDbContext dbContext,
        Guid capabilityGraphId,
        GraphNodeDto node,
        IReadOnlyList<Guid> requiresNodeIds,
        IReadOnlyList<Guid> requiredByNodeIds,
        int sortOrder,
        IReadOnlyList<NodeIllustrationRecord>? illustrations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(node);

        var nodeEntity = new CapabilityGraphNode
        {
            CapabilityGraphNodeId = node.NodeId != Guid.Empty ? node.NodeId : Guid.NewGuid(),
            CapabilityGraphId = capabilityGraphId,
            Name = node.Name,
            Description = node.Description,
            NodeType = (LearningNodeType)(int)node.NodeType,
            SortOrder = sortOrder,
            AcademicDefinition = node.AcademicDefinition,
            Interpretation = node.Interpretation,
            ExamplesJson = JsonSerializer.Serialize(node.Examples),
            ApplicationsJson = JsonSerializer.Serialize(node.Applications),
            ReferencesJson = JsonSerializer.Serialize(node.References),
            CreatedDate = DateTime.UtcNow
        };

        dbContext.CapabilityGraphNodes.Add(nodeEntity);

        foreach (var requiresId in requiresNodeIds)
        {
            dbContext.CapabilityGraphEdges.Add(new CapabilityGraphEdge
            {
                CapabilityGraphEdgeId = Guid.NewGuid(),
                CapabilityGraphId = capabilityGraphId,
                SourceNodeId = nodeEntity.CapabilityGraphNodeId,
                TargetNodeId = requiresId,
                RelationshipType = RelationshipType.Requires,
                CreatedDate = DateTime.UtcNow
            });
        }

        foreach (var requiredById in requiredByNodeIds)
        {
            dbContext.CapabilityGraphEdges.Add(new CapabilityGraphEdge
            {
                CapabilityGraphEdgeId = Guid.NewGuid(),
                CapabilityGraphId = capabilityGraphId,
                SourceNodeId = requiredById,
                TargetNodeId = nodeEntity.CapabilityGraphNodeId,
                RelationshipType = RelationshipType.Requires,
                CreatedDate = DateTime.UtcNow
            });
        }

        if (illustrations is not null)
        {
            foreach (var illustration in illustrations)
            {
                dbContext.CapabilityGraphNodeIllustrations.Add(new CapabilityGraphNodeIllustration
                {
                    CapabilityGraphNodeIllustrationId = Guid.NewGuid(),
                    CapabilityGraphNodeId = nodeEntity.CapabilityGraphNodeId,
                    StoragePath = illustration.StoragePath,
                    Prompt = illustration.Prompt,
                    Caption = illustration.Caption,
                    ImageModel = illustration.ImageModel,
                    Width = illustration.Width,
                    Height = illustration.Height,
                    Purpose = illustration.Purpose,
                    CreatedDate = DateTime.UtcNow
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return nodeEntity;
    }

    /// <summary>
    /// Permanently deletes ONE CapabilityGraphNode — Capability Studio's
    /// "delete this node" tool (2026-07-31), for cleaning up nodes whose
    /// content generation never completed (e.g. a backend crash mid-generation
    /// left the node persisted with no NodeExperienceBlueprint) or any node
    /// a professor simply wants gone. Irreversible — the caller must have
    /// already confirmed with the user (see DeleteGraphNodeModal.tsx).
    ///
    /// Delete order matters because of FK constraints on CapabilityGraphNode:
    /// 1. CapabilityGraphEdge — SourceNodeId/TargetNodeId are NoAction FKs
    ///    (behave like Restrict at the DB level), so both directions must be
    ///    removed explicitly first.
    /// 2. LearningSessionNode — CapabilityGraphNodeId is a Restrict FK. Only
    ///    the progress row(s) FOR THIS NODE are removed (cascading down to
    ///    that node's Steps/AssessmentResults/AssessmentRounds); the owning
    ///    LearningSession itself — and a student's progress on this
    ///    capability's OTHER nodes — is left untouched.
    /// 3. The node itself, which then DB-cascades to its Illustrations,
    ///    ExperienceBlueprints (-> Steps/Validations), KnowledgeChunks, and
    ///    KnowledgeExpansion cache row.
    /// Returns false if no node with that id exists.
    /// </summary>
    public async Task<bool> DeleteNodeAsync(
        HumanOsDbContext dbContext,
        Guid capabilityGraphNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var nodeExists = await dbContext.CapabilityGraphNodes
            .AsNoTracking()
            .AnyAsync(n => n.CapabilityGraphNodeId == capabilityGraphNodeId, cancellationToken);

        if (!nodeExists)
        {
            return false;
        }

        // SqlServerRetryingExecutionStrategy (EnableRetryOnFailure) refuses a
        // user-managed transaction directly — the strategy itself must own
        // and retry the whole atomic unit via ExecuteAsync (same pattern as
        // CapabilityService.DeleteAsync).
        var strategy = dbContext.Database.CreateExecutionStrategy();

        var rowsDeleted = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await dbContext.CapabilityGraphEdges
                    .Where(e => e.SourceNodeId == capabilityGraphNodeId || e.TargetNodeId == capabilityGraphNodeId)
                    .ExecuteDeleteAsync(cancellationToken);

                await dbContext.LearningSessionNodes
                    .Where(n => n.CapabilityGraphNodeId == capabilityGraphNodeId)
                    .ExecuteDeleteAsync(cancellationToken);

                var deleted = await dbContext.CapabilityGraphNodes
                    .Where(n => n.CapabilityGraphNodeId == capabilityGraphNodeId)
                    .ExecuteDeleteAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return deleted;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        return rowsDeleted > 0;
    }
}
