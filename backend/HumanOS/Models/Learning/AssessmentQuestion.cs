using System;

namespace HumanOS.Models.Learning;

/// <summary>
/// One dynamically-generated question within an <see cref="AssessmentRound"/>.
/// Never reused across rounds — the Memory Paradox spec explicitly rejects
/// a fixed question bank ("no queremos preguntas repetidas ni exámenes
/// estáticos").
/// </summary>
public class AssessmentQuestion
{
    /// <summary>Identificador único (GUID).</summary>
    public Guid AssessmentQuestionId { get; set; } = Guid.NewGuid();

    /// <summary>FK: AssessmentRound a la que pertenece esta pregunta.</summary>
    public Guid AssessmentRoundId { get; set; }

    /// <summary>Posición 1-5 de esta pregunta dentro de su ronda.</summary>
    public int QuestionIndex { get; set; }

    /// <summary>Tipo pedagógico de la pregunta.</summary>
    public AssessmentQuestionType QuestionType { get; set; }

    /// <summary>Texto de la pregunta, generado dinámicamente por AdaptiveAssessmentAgent.</summary>
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>Respuesta del estudiante. Null hasta que responda.</summary>
    public string? StudentAnswer { get; set; }

    /// <summary>Veredicto de corrección, derivado deterministicamente de ScoreContribution.</summary>
    public AssessmentQuestionCorrectness Correctness { get; set; } = AssessmentQuestionCorrectness.Pending;

    /// <summary>
    /// Puntaje bruto 0-100 que AdaptiveAssessmentAgent propuso para esta
    /// respuesta. <see cref="Correctness"/> siempre se deriva de este valor
    /// mediante umbrales fijos en código (nunca se confía directamente en
    /// el LLM) — misma regla "LLM propone, código decide" que
    /// AssessmentEvaluator.
    /// </summary>
    public int? ScoreContribution { get; set; }

    /// <summary>Retroalimentación específica y accionable para esta pregunta.</summary>
    public string? Feedback { get; set; }

    /// <summary>
    /// Etiqueta corta del error/confusión que reveló esta respuesta (ej.
    /// "confunde perímetro con área"), null si no se detectó ninguno.
    /// Alimenta la generación de la SIGUIENTE pregunta (esta ronda) y, si la
    /// ronda falla, las preguntas de la ronda siguiente — "atacar
    /// especialmente los errores detectados".
    /// </summary>
    public string? ObservedError { get; set; }

    /// <summary>
    /// FK opcional: ilustración generada bajo demanda para ESTA pregunta
    /// específica (CapabilityGraphNodeIllustration con
    /// Purpose=Assessment), o null si AdaptiveAssessmentAgent decidió que
    /// una imagen no aportaba valor a esta pregunta en particular
    /// (2026-07-20 — "cuando sea conveniente", nunca obligatoria). A
    /// diferencia de las ilustraciones de Hypothesis/Teaching/
    /// KnowledgeExpansion (una por nodo, reutilizada), esta es una por
    /// PREGUNTA — nunca se reutiliza, ya que cada pregunta es efímera y
    /// generada dinámicamente.
    /// </summary>
    public Guid? IllustrationId { get; set; }

    /// <summary>Fecha UTC de creación de la fila (cuando se generó la pregunta).</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Fecha UTC en que el estudiante respondió. Null mientras no responda.</summary>
    public DateTime? AnsweredDate { get; set; }

    // === Navigation Properties ===

    /// <summary>Referencia a la AssessmentRound padre.</summary>
    public virtual AssessmentRound? AssessmentRound { get; set; }

    /// <summary>Referencia a la ilustración generada para esta pregunta, si existe.</summary>
    public virtual Capabilities.Graph.CapabilityGraphNodeIllustration? Illustration { get; set; }
}
