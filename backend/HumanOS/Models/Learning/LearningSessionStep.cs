using System;
using HumanOS.Models.Capabilities.Graph;

namespace HumanOS.Models.Learning;

/// <summary>
/// Represents ONE real execution of a Memory Paradox step (Hypothesis,
/// Teaching, Recall, Production or Assessment) for a student, within a
/// <see cref="LearningSessionNode"/>. Reuses <see cref="ExperienceStepType"/>
/// (the same enum NodeExperienceBlueprintStep uses) so a step here maps 1:1
/// back to the blueprint step it executes.
/// </summary>
public class LearningSessionStep
{
    /// <summary>Identificador único (GUID).</summary>
    public Guid LearningSessionStepId { get; set; } = Guid.NewGuid();

    /// <summary>FK: LearningSessionNode al que pertenece este step.</summary>
    public Guid LearningSessionNodeId { get; set; }

    /// <summary>Tipo de step del Memory Paradox (Hypothesis/Teaching/Recall/Production/Assessment).</summary>
    public ExperienceStepType StepType { get; set; }

    /// <summary>Estado actual de este step.</summary>
    public LearningSessionStepStatus Status { get; set; } = LearningSessionStepStatus.NotStarted;

    /// <summary>Fecha UTC en que el estudiante empezó este step. Null mientras esté NotStarted.</summary>
    public DateTime? StartedDate { get; set; }

    /// <summary>Fecha UTC en que el estudiante terminó este step. Null mientras siga en curso.</summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>Fecha UTC de creación de la fila.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    /// <summary>Referencia al LearningSessionNode padre.</summary>
    public virtual LearningSessionNode? LearningSessionNode { get; set; }

    /// <summary>Respuestas/evidencia real que el estudiante produjo durante este step.</summary>
    public virtual ICollection<LearningEvidence> Evidence { get; set; } = new List<LearningEvidence>();
}
