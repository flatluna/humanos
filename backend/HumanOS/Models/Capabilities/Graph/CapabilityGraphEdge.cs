using System;

namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// Representa una arista (relación) entre dos nodos en un Capability Graph.
///
/// Una arista modela una dependencia (Requires) o una construcción incremental (BuildsOn)
/// entre dos conceptos o skills.
///
/// Ejemplo:
///   - "LoopsSkill --Requires--> VariablesSkill" (debes saber variables antes de loops)
///   - "MultiplicationConcept --BuildsOn--> AdditionConcept" (multiplicación se construye sobre suma)
/// </summary>
public class CapabilityGraphEdge
{
    /// <summary>Identificador único de la arista (GUID).</summary>
    public Guid CapabilityGraphEdgeId { get; set; } = Guid.NewGuid();

    /// <summary>FK: CapabilityGraph al que pertenece esta arista.</summary>
    public Guid CapabilityGraphId { get; set; }

    /// <summary>FK: Nodo origen de la arista.</summary>
    public Guid SourceNodeId { get; set; }

    /// <summary>FK: Nodo destino de la arista.</summary>
    public Guid TargetNodeId { get; set; }

    /// <summary>Tipo de relación (Requires o BuildsOn).</summary>
    public RelationshipType RelationshipType { get; set; } = RelationshipType.Requires;

    /// <summary>Fecha UTC de creación de la arista.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    /// <summary>Referencia al CapabilityGraph padre.</summary>
    public virtual CapabilityGraph? CapabilityGraph { get; set; }

    /// <summary>Nodo origen de esta arista.</summary>
    public virtual CapabilityGraphNode? SourceNode { get; set; }

    /// <summary>Nodo destino de esta arista.</summary>
    public virtual CapabilityGraphNode? TargetNode { get; set; }
}
