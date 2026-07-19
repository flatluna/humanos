namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// Enum que representa los tipos de nodos en un Capability Graph.
/// 
/// Los nodos representan conceptos, skills o capacidades atómicas dentro del grafo de aprendizaje.
/// NO incluimos "Capability" aquí porque el nodo ya vive dentro de una Capability,
/// evitando así confusión futura.
/// </summary>
public enum LearningNodeType
{
    /// <summary>Nodo que representa un concepto teórico (ej: "Variables", "Tipos de Datos").</summary>
    Concept = 0,

    /// <summary>Nodo que representa una habilidad práctica (ej: "Crear un bucle", "Depurar código").</summary>
    Skill = 1
}
