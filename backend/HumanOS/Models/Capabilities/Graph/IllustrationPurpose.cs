namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// What a <see cref="CapabilityGraphNodeIllustration"/> is meant to be used
/// for. Introduced 2026-07-18 to fix a real content bug: nodes previously got
/// only ONE illustration, generated as a "worked example" that already shows
/// the resolved answer (e.g. an arrow joining two groups into their combined
/// total) — perfectly fine for <see cref="ExperienceStepType.Teaching"/>, but
/// wrong for <see cref="ExperienceStepType.Hypothesis"/>, whose entire point
/// is asking the learner to predict BEFORE seeing the answer. Each node now
/// gets one illustration per purpose, and both GraphArchitectAgent (which
/// writes the Prompt) and ExperienceDesignerAgent (which picks which
/// illustration a step reuses) are constrained by this tag — enforced with a
/// deterministic code-level filter at blueprint-persistence time, not just
/// LLM instructions (see NodeExperienceBlueprintPersistenceService).
/// </summary>
public enum IllustrationPurpose
{
    /// <summary>Depicts only the "before"/given state (e.g. two separate,
    /// uncombined groups) — must NEVER show a resolved/combined/computed
    /// result. Reused exclusively by the Hypothesis step.</summary>
    Hypothesis = 0,

    /// <summary>A full worked example that DOES show the concept resolved/
    /// applied with real values (e.g. the groups joined into their total).
    /// Reused exclusively by the Teaching step.</summary>
    Teaching = 1,

    /// <summary>A diagram generated on-demand by KnowledgeExpansionAgent
    /// (2026-07-20) when a learner clicks "Profundizar" on a node — never
    /// generated up-front during capability creation, never reused by the
    /// standard Hypothesis/Teaching steps. See
    /// <see cref="CapabilityGraphNodeKnowledgeExpansion"/>.</summary>
    KnowledgeExpansion = 2,

    /// <summary>An illustration generated on-demand by AdaptiveAssessmentAgent
    /// (2026-07-20) for ONE dynamically-generated Assessment question, only
    /// when the agent decided a visual genuinely helps that specific
    /// question (e.g. a scenario-based Application/Transfer/ErrorDetection
    /// task) — never generated for every question, never reused across
    /// questions. See <see cref="Models.Learning.AssessmentQuestion"/>.</summary>
    Assessment = 3,

    /// <summary>An illustration generated on-demand by
    /// BlueprintStepEditorAgent (2026-07-21) via Capability Studio's
    /// "Edición" preview-mode reviewer tool. Unlike Hypothesis/Teaching,
    /// this is NOT restricted to a single step type — a reviewer can ask
    /// for (or replace) an illustration on ANY of the 5 blueprint steps
    /// (e.g. Recall, Production, Assessment) via a free-text instruction.
    /// See <see cref="Services.BlueprintReviewService"/>.</summary>
    BlueprintReviewEdit = 4,

    /// <summary>An illustration generated on-demand by TutorAgentV2
    /// (2026-07-21) for ONE dynamic Recall follow-up turn, when the Tutor
    /// varies the concrete scenario (different numbers/objects) enough
    /// that the step's previously-shown illustration would now contradict
    /// the new question it just asked (e.g. a new question about "3
    /// grupos de 6 perritos" after an earlier turn's illustration showed a
    /// different quantity). Ephemeral in spirit — tied to one specific
    /// Recall question, not reused across the whole step like
    /// Hypothesis/Teaching illustrations. See
    /// <see cref="Services.TutorService.SubmitRecallAttemptAsync"/>.</summary>
    TutorRecallTurn = 5
}
