namespace HumanOS.Agents.Runtime;

/// <summary>
/// Lightweight completeness check for a single Recall attempt (fixed
/// 2026-07-16 — implements iterative retrieval practice per explicit user
/// request: "quiero que el usuario pueda repasar o aprender con varias
/// iteraciones de preguntas y respuestas", grounded in "The Memory
/// Paradox" (Oakley et al., 2025)'s emphasis on genuine retrieval practice
/// over one-shot recognition). A single Recall submission that silently
/// advances regardless of completeness wastes the retrieval-practice
/// opportunity — this type lets the Runtime give the learner one more
/// bounded attempt with a Socratic, answer-free follow-up instead.
/// </summary>
/// <remarks>
/// Deliberately DISTINCT from <see cref="RuntimeAssessmentResult"/>: this
/// is NOT a formal TargetMetric verification and never claims Recall (as
/// a metric) is verified — it is a lighter nudge scoped only to deciding
/// whether the Recall stage should loop once more before moving on to
/// Prediction. The real, formal TargetMetric verification remains
/// <see cref="RuntimeStage.Assessment"/>'s exclusive job.
/// </remarks>
public sealed class RecallCheckResult
{
    /// <summary>True when the recall attempt captures the main
    /// stages/facts reasonably well (not necessarily perfectly, not
    /// necessarily verbatim) — false triggers one more bounded retrieval
    /// attempt (see <see cref="Agentic.Runtime.RuntimeSessionWorkflowFactory.MaxRecallRetries"/>).</summary>
    public bool IsSufficient { get; set; }

    /// <summary>Answer-free, Socratic follow-up question/hint pointing the
    /// learner toward what they likely missed, WITHOUT stating the missed
    /// fact directly (prediction-error style — e.g. "cubriste los pasos
    /// principales, ¿qué crees que pasa con la temperatura del agua?"
    /// instead of revealing the temperature). Only meaningful when
    /// <see cref="IsSufficient"/> is <see langword="false"/>.</summary>
    public string FollowUpPrompt { get; set; } = string.Empty;

    /// <summary>False when the learner's submission was NOT actually an
    /// attempt at the recall prompt — e.g. a clarifying question ("¿qué
    /// significa normalizar?"), confusion, or an off-topic remark (fixed
    /// 2026-07-17 — real gap found live: the learner asked a question and
    /// the Runtime silently treated it as a failed recall attempt and
    /// burned a retry moving on, ignoring the question entirely — "el
    /// usuario hizo pregunta, el agente ignoró, pasa a la siguiente...
    /// eso no me gusta"). When <see langword="false"/>,
    /// <see cref="FollowUpPrompt"/> should briefly answer/address what the
    /// learner asked (vocabulary, instructions — never the actual content
    /// being retrieved) and then re-invite them to attempt the ORIGINAL
    /// recall prompt again. The retry executor does NOT count this turn
    /// against the learner's bounded retry budget when this is
    /// <see langword="false"/> — asking a genuine question should never
    /// cost them a retrieval-practice attempt. Defaults to
    /// <see langword="true"/> (a genuine attempt) for backward
    /// compatibility with any code that doesn't set it.</summary>
    public bool IsGenuineAttempt { get; set; } = true;

    /// <summary>Rough estimate (0-100) of how complete/correct this
    /// attempt was compared to the real content (fixed 2026-07-17 —
    /// explicit user request: "pon el numero de iteracion... y el
    /// porcentaje de error o que tan exacto fue, como esta construyendo
    /// su memoria"). NOT a formal grade — a lightweight, visible signal so
    /// the learner can see their own retrieval practice improving across
    /// attempts. Meaningless (leave at 0) when <see cref="IsGenuineAttempt"/>
    /// is <see langword="false"/>.</summary>
    public int AccuracyPercentage { get; set; }
}
