using System;
using HumanOS.Models.Capabilities;

namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// Representa un Capability Graph completo.
///
/// Un grafo contiene nodos (conceptos/skills) y aristas (relaciones) que modelan las
/// dependencias y el orden de aprendizaje para una Capability.
/// 
/// NOTA IMPORTANTE (PASO 1):
/// En PASO 1, CapabilityGraph es una estructura de datos pura sin generación automática.
/// El grafo se crea y actualiza manualmente o a través de herramientas externas.
/// En PASO 2, GraphArchitectAgent generará estos grafos automáticamente.
/// </summary>
public class CapabilityGraph
{
    /// <summary>Identificador único del grafo (GUID).</summary>
    public Guid CapabilityGraphId { get; set; } = Guid.NewGuid();

    /// <summary>FK: Capability a la que pertenece este grafo.</summary>
    public Guid CapabilityId { get; set; }

    /// <summary>Nombre descriptivo del grafo.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Descripción detallada de la estructura y propósito del grafo.</summary>
    public string? Description { get; set; }

    /// <summary>Document-wide executive summary of the ENTIRE source
    /// material this capability was built from (2026-07-20 — see
    /// <see cref="HumanOS.Agents.Studio.DocumentContextAgent"/> and
    /// /memories/repo/tutor-document-wide-context-gap.md), generated ONCE
    /// per capability from the merged <see cref="Agents.Studio.CuratedCorpus"/>.
    /// Fed to TutorAgentV2 (via TutorTurnRequest.DocumentSummary) ONLY when
    /// the student actively asks the Tutor a free-form question
    /// (Teaching/Production mode) — never during Recall grading or
    /// automatic content presentation. Null when the extraction agent
    /// isn't configured or the extraction call failed (best-effort).</summary>
    public string? ExecutiveSummary { get; set; }

    /// <summary>JSON array of key named entities (people, companies,
    /// laws/regulations, roles, proper names, ...) explicitly grounded in
    /// the source material — see <see cref="Agents.Studio.DocumentEntityDto"/>.
    /// Same generation/usage rules as <see cref="ExecutiveSummary"/>.</summary>
    public string? KeyEntitiesJson { get; set; }

    /// <summary>Fecha UTC de creación del grafo.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    /// <summary>Referencia a la Capability padre.</summary>
    public virtual Capability? Capability { get; set; }

    /// <summary>Colección de todos los nodos en este grafo.</summary>
    public virtual ICollection<CapabilityGraphNode> Nodes { get; set; } = new List<CapabilityGraphNode>();

    /// <summary>Colección de todas las aristas en este grafo.</summary>
    public virtual ICollection<CapabilityGraphEdge> Edges { get; set; } = new List<CapabilityGraphEdge>();
}
