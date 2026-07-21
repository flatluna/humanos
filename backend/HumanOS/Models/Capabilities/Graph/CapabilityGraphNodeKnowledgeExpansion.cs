using System;

namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// Cached, on-demand "profundizar" (deepen knowledge) result for ONE
/// CapabilityGraphNode — generated the first time a learner clicks the
/// "Profundizar" button in the Teaching step (2026-07-20). Combines the
/// LLM's own knowledge with a real-time Grounding-with-Bing-Search lookup
/// (always attempted when configured, regardless of the node's
/// NeedsCurrentInfo flag — unlike the Studio creation-time pipeline, this is
/// an explicit, deliberate learner action, so the cost is always justified).
///
/// Shared across ALL learners of the same node (not per-person) — one row
/// per CapabilityGraphNodeId, generated once and reused on subsequent
/// clicks, same "generate once, serve many" pattern as
/// CapabilityGraphNodeIllustration.
/// </summary>
public class CapabilityGraphNodeKnowledgeExpansion
{
    /// <summary>Identificador único (GUID).</summary>
    public Guid CapabilityGraphNodeKnowledgeExpansionId { get; set; } = Guid.NewGuid();

    /// <summary>FK: CapabilityGraphNode al que pertenece esta ampliación. Único (1 fila por nodo).</summary>
    public Guid CapabilityGraphNodeId { get; set; }

    /// <summary>Contenido HTML ampliado (mismo formato semántico simple que
    /// NodeExperienceBlueprintStep.Content), con citas "&lt;a href&gt;" cuando
    /// vinieron de Bing Grounding.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>FK opcional: diagrama generado para acompañar esta ampliación
    /// (CapabilityGraphNodeIllustration con Purpose=KnowledgeExpansion). Null
    /// si el agente decidió que un diagrama no aportaba valor para este nodo.</summary>
    public Guid? DiagramIllustrationId { get; set; }

    /// <summary>Fecha UTC de creación.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    /// <summary>Referencia al CapabilityGraphNode padre.</summary>
    public virtual CapabilityGraphNode? CapabilityGraphNode { get; set; }

    /// <summary>Referencia al diagrama generado, si existe.</summary>
    public virtual CapabilityGraphNodeIllustration? DiagramIllustration { get; set; }
}
