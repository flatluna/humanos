using System;

namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// Representa un nodo de aprendizaje dentro de un Capability Graph.
///
/// Un nodo es una unidad atómica de conocimiento o habilidad (Concept o Skill).
/// Su posición en el grafo refleja sus dependencias con otros nodos.
///
/// SortOrder permite visualizar el grafo en un orden pedagógico lineal (1. Concept A, 2. Skill B, etc.)
/// sin perder la estructura DAG subyacente.
/// 
/// PASO 2 Future Bridge:
/// CapabilityModule.GraphNodeId (cuando exista) vinculará cada CapabilityModule a su nodo correspondiente,
/// permitiendo que el Runtime ejecute módulos en orden topológico del grafo.
/// </summary>
public class CapabilityGraphNode
{
    /// <summary>Identificador único del nodo (GUID).</summary>
    public Guid CapabilityGraphNodeId { get; set; } = Guid.NewGuid();

    /// <summary>FK: CapabilityGraph al que pertenece este nodo.</summary>
    public Guid CapabilityGraphId { get; set; }

    /// <summary>Nombre del concepto o skill (ej: "Variables", "Loops", "Debugging").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Descripción detallada del concepto o skill.</summary>
    public string? Description { get; set; }

    /// <summary>Tipo de nodo: Concept (teoría) o Skill (práctica).</summary>
    public LearningNodeType NodeType { get; set; } = LearningNodeType.Concept;

    /// <summary>
    /// Orden de visualización/presentación del nodo en la UI (1, 2, 3, ...).
    /// Permite mostrar el grafo como una secuencia pedagógicamente ordenada sin forzar un árbol.
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// Definición académica/formal del concepto o skill, fundamentada en el corpus.
    /// Este nodo es autocontenido: NUNCA se separa en un nodo "Definición" aparte.
    /// </summary>
    public string? AcademicDefinition { get; set; }

    /// <summary>Interpretación en lenguaje llano de la misma idea (cómo la explicaría un aprendiz).</summary>
    public string? Interpretation { get; set; }

    /// <summary>Ejemplos concretos que ilustran el concepto o skill, serializados como JSON (List&lt;string&gt;).</summary>
    public string? ExamplesJson { get; set; }

    /// <summary>Aplicaciones reales del concepto o skill, serializadas como JSON (List&lt;string&gt;).</summary>
    public string? ApplicationsJson { get; set; }

    /// <summary>Tags de los chunks del corpus a los que este nodo es trazable, serializados como JSON (List&lt;string&gt;).</summary>
    public string? ReferencesJson { get; set; }

    /// <summary>Fecha UTC de creación del nodo.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    /// <summary>Referencia al CapabilityGraph padre.</summary>
    public virtual CapabilityGraph? CapabilityGraph { get; set; }

    /// <summary>Colección de aristas SALIENTES desde este nodo (este nodo es origen).</summary>
    public virtual ICollection<CapabilityGraphEdge> OutgoingEdges { get; set; } = new List<CapabilityGraphEdge>();

    /// <summary>Colección de aristas ENTRANTES hacia este nodo (este nodo es destino).</summary>
    public virtual ICollection<CapabilityGraphEdge> IncomingEdges { get; set; } = new List<CapabilityGraphEdge>();

    /// <summary>Ilustraciones (imágenes) generadas para este nodo. Metadata solamente — el binario vive en Data Lake.</summary>
    public virtual ICollection<CapabilityGraphNodeIllustration> Illustrations { get; set; } = new List<CapabilityGraphNodeIllustration>();

    /// <summary>
    /// Blueprints pedagógicos (Paso 3) que describen cómo enseñar este nodo.
    /// Un nodo puede tener múltiples blueprints (Standard, Advanced, Visual Learning, ...).
    /// </summary>
    public virtual ICollection<NodeExperienceBlueprint> ExperienceBlueprints { get; set; } = new List<NodeExperienceBlueprint>();

    /// <summary>Embedded knowledge chunks of this node's own content (2026-07-20),
    /// used for cross-node semantic search (RAG) — see
    /// <see cref="CapabilityGraphNodeKnowledgeChunk"/> and
    /// <see cref="Services.NodeKnowledgeIndexService"/>.</summary>
    public virtual ICollection<CapabilityGraphNodeKnowledgeChunk> KnowledgeChunks { get; set; } = new List<CapabilityGraphNodeKnowledgeChunk>();
}
