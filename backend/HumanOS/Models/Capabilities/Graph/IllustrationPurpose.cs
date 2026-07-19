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
    Teaching = 1
}
