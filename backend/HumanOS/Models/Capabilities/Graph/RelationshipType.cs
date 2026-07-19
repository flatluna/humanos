namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// Enum que representa los tipos de relaciones entre nodos en un Capability Graph.
/// </summary>
public enum RelationshipType
{
    /// <summary>El nodo origen requiere completamente el nodo destino para poder aprender.</summary>
    Requires = 0,

    /// <summary>El nodo origen se construye sobre o amplía el conocimiento del nodo destino.</summary>
    BuildsOn = 1
}
