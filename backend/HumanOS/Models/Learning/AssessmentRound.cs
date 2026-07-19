using System;

namespace HumanOS.Models.Learning;

/// <summary>
/// One dynamic-assessment attempt cycle for a node's Assessment step. Per
/// the Memory Paradox principle, Assessment is NEVER a fixed question bank
/// — each round is exactly 5 questions generated fresh by
/// <see cref="Agents.Runtime.AdaptiveAssessmentAgent"/> and asked ONE AT A
/// TIME. APPEND-ONLY: a Failed round's questions are never reused — a
/// brand-new round with 5 NEW questions starts automatically instead
/// (see <see cref="Services.AdaptiveAssessmentEngine"/>).
/// </summary>
public class AssessmentRound
{
    /// <summary>Identificador único (GUID).</summary>
    public Guid AssessmentRoundId { get; set; } = Guid.NewGuid();

    /// <summary>FK: LearningSessionNode cuyo Assessment step corresponde a esta ronda.</summary>
    public Guid LearningSessionNodeId { get; set; }

    /// <summary>Número de intento para este nodo (1 = primer intento, 2 = primer reintento, ...).</summary>
    public int RoundNumber { get; set; }

    /// <summary>Estado actual de la ronda.</summary>
    public AssessmentRoundStatus Status { get; set; } = AssessmentRoundStatus.InProgress;

    /// <summary>Promedio de los ScoreContribution de las 5 preguntas (0-100). Null mientras InProgress.</summary>
    public int? FinalScore { get; set; }

    /// <summary>Fecha UTC de creación de la ronda.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha UTC en que se respondió la 5ta pregunta. Null mientras InProgress.</summary>
    public DateTime? CompletedDate { get; set; }

    // === Navigation Properties ===

    /// <summary>Referencia al LearningSessionNode padre.</summary>
    public virtual LearningSessionNode? LearningSessionNode { get; set; }

    /// <summary>Las (hasta) 5 preguntas dinámicas de esta ronda, en orden.</summary>
    public virtual ICollection<AssessmentQuestion> Questions { get; set; } = new List<AssessmentQuestion>();
}
