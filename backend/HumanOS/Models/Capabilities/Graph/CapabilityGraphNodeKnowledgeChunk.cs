using System;
using Microsoft.Data.SqlTypes;

namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// One embedded chunk of a <see cref="CapabilityGraphNode"/>'s own content
/// (AcademicDefinition, Interpretation, one Example, one Application, or —
/// opportunistically, once generated — its cached
/// <see cref="CapabilityGraphNodeKnowledgeExpansion"/> "Profundizar"
/// content), used for cross-node semantic search (RAG) at Tutor-turn time
/// (2026-07-20).
///
/// This is the V2 ("Graph/Blueprint") pipeline's OWN knowledge-chunk table
/// — deliberately NOT a reuse of V1's <see cref="CapabilityKnowledgeChunk"/>
/// (that one is FK'd to CapabilityModuleId, the old Script/Module model).
/// Same embedding model/service (<see cref="Agents.Studio.CapabilityEmbeddingService"/>)
/// and native Azure SQL <c>vector(1536)</c> column pattern is reused
/// though — see <see cref="Services.NodeKnowledgeIndexService"/> for how
/// these rows are produced and searched.
///
/// Purpose: lets the Tutor answer a student's question that names a
/// specific fact living in a DIFFERENT node of the same
/// <see cref="CapabilityGraph"/> than the one currently being taught (e.g.
/// "¿qué número de póliza es esta, de qué empresa y cuándo expira?" when
/// the current node is about a general purchasing competency, but that
/// concrete detail was only given as an Example on a sibling node) — a
/// capability-wide executive summary alone can't hold verbatim facts like
/// this since summaries are intentionally lossy.
/// </summary>
public class CapabilityGraphNodeKnowledgeChunk
{
    /// <summary>Identificador único del chunk (GUID).</summary>
    public Guid CapabilityGraphNodeKnowledgeChunkId { get; set; } = Guid.NewGuid();

    /// <summary>FK: CapabilityGraphNode al que pertenece este chunk.</summary>
    public Guid CapabilityGraphNodeId { get; set; }

    /// <summary>Denormalized (no FK — see <see cref="Data.Configurations.CapabilityGraphNodeKnowledgeChunkConfiguration"/>
    /// for why) copy of the parent node's CapabilityGraphId, so a semantic
    /// search can be scoped to "every node in this same graph" with a plain
    /// indexed column filter instead of a join.</summary>
    public Guid CapabilityGraphId { get; set; }

    /// <summary>Which field of the node this chunk came from — e.g.
    /// "AcademicDefinition", "Interpretation", "Example", "Application",
    /// "KnowledgeExpansion". Informational only (not used to filter
    /// search), useful for debugging/inspection.</summary>
    public string SourceField { get; set; } = string.Empty;

    /// <summary>Order among chunks from the SAME SourceField of the SAME
    /// node (e.g. Example #0, Example #1, ...).</summary>
    public int SortOrder { get; set; }

    /// <summary>The raw chunk text that was embedded.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Native Azure SQL VECTOR(1536) embedding of <see cref="Content"/>
    /// (same embedding deployment as V1's CapabilityKnowledgeChunk).</summary>
    public SqlVector<float> Embedding { get; set; }

    /// <summary>Fecha UTC de creación del chunk.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    /// <summary>Referencia al CapabilityGraphNode padre.</summary>
    public virtual CapabilityGraphNode? CapabilityGraphNode { get; set; }
}
