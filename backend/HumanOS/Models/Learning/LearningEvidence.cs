using System;

namespace HumanOS.Models.Learning;

/// <summary>
/// One real response/answer a student gave during a
/// <see cref="LearningSessionStep"/> (e.g. their Hypothesis guess, their
/// Recall answer, their Production submission, their Assessment answer).
/// APPEND-ONLY: a step can accumulate more than one piece of evidence
/// (e.g. a retry), never overwritten.
/// </summary>
public class LearningEvidence
{
    /// <summary>Identificador único (GUID).</summary>
    public Guid LearningEvidenceId { get; set; } = Guid.NewGuid();

    /// <summary>FK: LearningSessionStep durante el cual se produjo esta evidencia.</summary>
    public Guid LearningSessionStepId { get; set; }

    /// <summary>Respuesta real del estudiante (texto libre).</summary>
    public string StudentResponse { get; set; } = string.Empty;

    /// <summary>
    /// Pregunta/pista que dio el TutorAgent inmediatamente antes de esta
    /// respuesta (null si esta evidencia no fue precedida por una
    /// interacción del Tutor). Junto con <see cref="StudentResponse"/>,
    /// filas consecutivas de esta tabla (ordenadas por CreatedDate) para el
    /// mismo LearningSessionStepId reconstruyen el historial completo de la
    /// conversación — el TutorAgent no guarda memoria propia, lee este log.
    /// </summary>
    public string? TutorPrompt { get; set; }

    /// <summary>
    /// Score efímero 0-100 que el TutorAgent asignó a ESTE intento (solo
    /// aplica en steps tipo Recall). NUNCA se confunde ni se copia al
    /// veredicto formal en <see cref="LearningAssessmentResult.Score"/> —
    /// ese es el único que gatea el grafo/progresión. Null si no aplica.
    /// </summary>
    public int? TutorScore { get; set; }

    /// <summary>Fecha UTC de creación de la fila.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    /// <summary>Referencia al LearningSessionStep padre.</summary>
    public virtual LearningSessionStep? LearningSessionStep { get; set; }
}
