namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Which pedagogical situation triggered this Tutor turn — determines both
/// which HARD RULE applies (e.g. RecallScore only set for Recall) and how
/// <see cref="TutorTurnExecutor"/> builds the prompt from
/// <see cref="TutorTurnRequest"/>. See <see cref="Agents.Runtime.TutorAgentV2"/>'s
/// Instructions for the exact per-mode behavior contract.
/// </summary>
public enum TutorInteractionMode
{
    /// <summary>Student didn't understand the step's Teaching content on
    /// first pass — Tutor re-explains it differently.</summary>
    Teaching,

    /// <summary>Student is attempting to recall a concept unaided — Tutor
    /// scores THIS attempt (ephemeral, RecallScore) and gives the next
    /// hint/Socratic question if not yet mastered.</summary>
    Recall,

    /// <summary>Student is producing/applying something — Tutor asks
    /// Socratic questions, never solves the task.</summary>
    Production,

    /// <summary>Formal Assessment feedback already exists
    /// (AssessmentEvaluatorAgent's verdict) — Tutor translates it into
    /// actionable, supportive language. Read-only: never re-judges.</summary>
    AssessmentFeedback
}

/// <summary>
/// One turn of prior Tutor/student exchange, reconstructed from a
/// <see cref="Models.Learning.LearningEvidence"/> row. The Tutor has no
/// memory of its own — this list, built fresh from the DB on every call, IS
/// its memory.
/// </summary>
public sealed class TutorTurnHistoryEntry
{
    /// <summary>The Tutor's question/hint that preceded StudentResponse —
    /// null if this row wasn't preceded by a Tutor interaction.</summary>
    public string? TutorPrompt { get; set; }

    public string StudentResponse { get; set; } = string.Empty;
}

/// <summary>
/// Input to <see cref="TutorWorkflowFactory"/>'s single Executor — one
/// on-demand Tutor interaction for one <see cref="Models.Learning.LearningSessionStep"/>.
/// Built by <see cref="Services.TutorService"/> from the database; the
/// Workflow itself never touches EF Core.
/// </summary>
public sealed class TutorTurnRequest
{
    public TutorInteractionMode Mode { get; set; }

    /// <summary>The relevant NodeExperienceBlueprintStep's Content — the
    /// ONLY domain-knowledge source the Tutor is allowed to draw from for
    /// Teaching/Recall/Production modes.</summary>
    public string StepContent { get; set; } = string.Empty;

    /// <summary>CapabilityGraphNodeIllustration(s) this step references
    /// (resolved the same way
    /// <see cref="Services.InstructorRuntimeOrchestrator.GetCurrentStepAsync"/>
    /// does). The Tutor LLM only reads each entry's Caption (never the image
    /// bytes) so it can explicitly point the student back at "the
    /// illustration" instead of ignoring it — but the full ref (including
    /// StoragePath) flows through unchanged to <see cref="TutorTurnResult"/>
    /// so the caller/frontend can actually render the image alongside the
    /// Tutor's text. Empty for AssessmentFeedback mode (no step content is
    /// loaded there either).</summary>
    public List<TutorIllustrationRef> Illustrations { get; set; } = [];

    /// <summary>Prior exchanges for this step, oldest first — reconstructed
    /// from LearningEvidence, ordered by CreatedDate.</summary>
    public List<TutorTurnHistoryEntry> History { get; set; } = [];

    /// <summary>What the student just said/submitted this turn.</summary>
    public string StudentMessage { get; set; } = string.Empty;

    /// <summary>Only for Mode = AssessmentFeedback: the raw
    /// AssessmentEvaluatorAgent feedback text to translate. Null otherwise.</summary>
    public string? RawAssessmentFeedback { get; set; }
}

/// <summary>
/// Output of <see cref="TutorWorkflowFactory"/>'s single Executor — the
/// Tutor's response for this turn plus the token usage of the LLM call
/// that produced it (same shape convention as every other agent's *Result
/// type, e.g. AssessmentEvaluatorAgent.EvaluationResult).
/// </summary>
public sealed class TutorTurnResult
{
    public Agents.Runtime.TutorTurnResponse Response { get; set; } = null!;

    /// <summary>Passed straight through from the request — the actual
    /// image(s) (StoragePath into Azure Data Lake) the Tutor's Message
    /// referenced in text, for the caller/frontend to render alongside it.</summary>
    public List<TutorIllustrationRef> Illustrations { get; set; } = [];

    public Agents.Studio.AgentTokenUsage TokenUsage { get; set; } = null!;
}

/// <summary>A CapabilityGraphNodeIllustration reference resolved for one
/// Tutor turn — same shape as
/// <see cref="Services.InstructorRuntimeOrchestrator.IllustrationRef"/>,
/// duplicated here (rather than referenced) so the Agentic.Runtime layer
/// doesn't take a compile-time dependency on the Services layer.</summary>
public sealed class TutorIllustrationRef
{
    public Guid CapabilityGraphNodeIllustrationId { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public string? Caption { get; set; }
}
