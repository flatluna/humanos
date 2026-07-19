using System;
using HumanOS.Models.Capabilities.Graph;

namespace HumanOS.Models.Learning;

/// <summary>
/// Represents a student's progress through ONE CapabilityGraphNode within a
/// <see cref="LearningSession"/> — which NodeExperienceBlueprint is being
/// executed for that node, and how far the student got.
/// </summary>
public class LearningSessionNode
{
    /// <summary>Identificador único (GUID).</summary>
    public Guid LearningSessionNodeId { get; set; } = Guid.NewGuid();

    /// <summary>FK: LearningSession a la que pertenece este progreso de nodo.</summary>
    public Guid LearningSessionId { get; set; }

    /// <summary>FK: CapabilityGraphNode que el estudiante está aprendiendo.</summary>
    public Guid CapabilityGraphNodeId { get; set; }

    /// <summary>FK: NodeExperienceBlueprint específico que se está ejecutando para este nodo.</summary>
    public Guid NodeExperienceBlueprintId { get; set; }

    /// <summary>Estado actual del progreso en este nodo.</summary>
    public LearningSessionNodeStatus Status { get; set; } = LearningSessionNodeStatus.NotStarted;

    /// <summary>Fecha UTC en que el estudiante empezó este nodo. Null mientras esté NotStarted.</summary>
    public DateTime? StartedDate { get; set; }

    /// <summary>Fecha UTC en que el estudiante terminó (Completed o Failed) este nodo. Null mientras siga en curso.</summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>Fecha UTC de creación de la fila.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    /// <summary>Referencia a la LearningSession padre.</summary>
    public virtual LearningSession? LearningSession { get; set; }

    /// <summary>Referencia al CapabilityGraphNode que se está aprendiendo.</summary>
    public virtual CapabilityGraphNode? CapabilityGraphNode { get; set; }

    /// <summary>Referencia al NodeExperienceBlueprint que se está ejecutando.</summary>
    public virtual NodeExperienceBlueprint? NodeExperienceBlueprint { get; set; }

    /// <summary>Ejecuciones reales de cada step del Memory Paradox (Hypothesis..Assessment) para este nodo.</summary>
    public virtual ICollection<LearningSessionStep> Steps { get; set; } = new List<LearningSessionStep>();

    /// <summary>Resultado(s) del Assessment de este nodo — normalmente uno, pero se permite reintento.</summary>
    public virtual ICollection<LearningAssessmentResult> AssessmentResults { get; set; } = new List<LearningAssessmentResult>();
}
