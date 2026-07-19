namespace HumanOS.Models.Learning;

/// <summary>
/// The pedagogical kind of one dynamically-generated Assessment question.
/// <see cref="MultipleChoice"/> must stay a MINORITY within a round (the
/// engine enforces at most 1 per 5-question round) — the Memory Paradox
/// spec explicitly rejects an assessment that leans on recognition
/// (ABCD) over real retrieval/reasoning.
/// </summary>
public enum AssessmentQuestionType
{
    /// <summary>Recuperación activa — recall without cues.</summary>
    ActiveRecall = 0,

    /// <summary>Comprensión — explain/paraphrase the concept.</summary>
    Comprehension = 1,

    /// <summary>Aplicación — use the concept in a new concrete situation.</summary>
    Application = 2,

    /// <summary>Detección de errores — spot what's wrong in a flawed example.</summary>
    ErrorDetection = 3,

    /// <summary>Transferencia — apply the concept in an unfamiliar/different context.</summary>
    Transfer = 4,

    /// <summary>Producción — construct/create something demonstrating mastery.</summary>
    Production = 5,

    /// <summary>Opción múltiple — used only occasionally, never as the majority.</summary>
    MultipleChoice = 6
}
