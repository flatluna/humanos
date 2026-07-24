using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;

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
    public async Task<CapabilityGraph> PersistAsync(
        HumanOsDbContext dbContext,
        Guid capabilityId,
        CapabilityGraphResponse graph,
        IReadOnlyList<NodeIllustrationRecord>? illustrations,
        CancellationToken cancellationToken = default,
        string? executiveSummary = null,
        string? keyEntitiesJson = null,
        string? coverImageStoragePath = null)
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
}
