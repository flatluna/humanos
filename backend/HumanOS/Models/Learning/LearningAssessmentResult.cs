using System;

namespace HumanOS.Models.Learning;

/// <summary>
/// The final outcome of the Assessment step for one
/// <see cref="LearningSessionNode"/>. APPEND-ONLY: a retry produces a NEW
/// row rather than overwriting the previous attempt, preserving full
/// assessment history for the node.
/// </summary>
public class LearningAssessmentResult
{
    /// <summary>Identificador único (GUID).</summary>
    public Guid LearningAssessmentResultId { get; set; } = Guid.NewGuid();

    /// <summary>FK: LearningSessionNode cuyo Assessment fue evaluado.</summary>
    public Guid LearningSessionNodeId { get; set; }

    /// <summary>Puntaje obtenido, 0-100.</summary>
    public int Score { get; set; }

    /// <summary>Si el estudiante aprobó el Assessment de este nodo.</summary>
    public bool Passed { get; set; }

    /// <summary>Retroalimentación textual sobre el desempeño del estudiante.</summary>
    public string? Feedback { get; set; }

    /// <summary>Fecha UTC de creación de la fila.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    /// <summary>Referencia al LearningSessionNode padre.</summary>
    public virtual LearningSessionNode? LearningSessionNode { get; set; }
}
