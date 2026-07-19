using System;

namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// A reusable PEDAGOGICAL TEMPLATE describing how to teach one
/// CapabilityGraphNode — the "Experience Layer" of the Knowledge Graph
/// (Paso 3, see /memories/repo/paso2-graph-persistence-completed.md for the
/// Knowledge Layer this builds on).
///
/// Deliberately NOT named "LearningExperience"/"LearningSession": those names
/// imply a live, in-progress student session. A NodeExperienceBlueprint has
/// no student, no progress, no attempts, no state — it is only a recipe that
/// Instructor later turns into a real CapabilityModule, which the Runtime
/// (RuntimeSession/StudentEvidence/Assessment) executes for a real learner.
///
/// A node may have multiple blueprints over time (Standard, Advanced, Visual
/// Learning, Kids Version, ...) — hence the 1:N relationship with
/// CapabilityGraphNode.
/// </summary>
public class NodeExperienceBlueprint
{
    /// <summary>Identificador único del blueprint (GUID).</summary>
    public Guid NodeExperienceBlueprintId { get; set; } = Guid.NewGuid();

    /// <summary>FK: CapabilityGraphNode que este blueprint enseña.</summary>
    public Guid CapabilityGraphNodeId { get; set; }

    /// <summary>Nombre descriptivo, p.ej. "Suma - Standard Learning Blueprint".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Descripción breve del enfoque pedagógico de este blueprint.</summary>
    public string? Description { get; set; }

    /// <summary>Versión del blueprint (permite iterar/reemplazar sin perder historial).</summary>
    public int Version { get; set; } = 1;

    /// <summary>Fecha UTC de creación del blueprint.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    /// <summary>Referencia al CapabilityGraphNode padre.</summary>
    public virtual CapabilityGraphNode? CapabilityGraphNode { get; set; }

    /// <summary>
    /// Los 5 pasos del Memory Paradox (Hypothesis, Teaching, Recall,
    /// Production, Assessment), en ese orden fijo.
    /// </summary>
    public virtual ICollection<NodeExperienceBlueprintStep> Steps { get; set; } = new List<NodeExperienceBlueprintStep>();

    /// <summary>
    /// Historial APPEND-ONLY de corridas de BlueprintValidatorAgent (Paso 4)
    /// sobre este blueprint — puede haber más de una si el blueprint se
    /// regenera y se vuelve a validar.
    /// </summary>
    public virtual ICollection<BlueprintValidation> Validations { get; set; } = new List<BlueprintValidation>();
}
