namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// The pedagogical step types of the "Memory Paradox" pattern — the
/// fixed, official teaching sequence every <see cref="NodeExperienceBlueprint"/>
/// must follow:
///
///   Hypothesis → Teaching → Recall → Production → Assessment
///
/// The order of these 5 core steps is never changed. An OPTIONAL 6th step,
/// <see cref="DeepDive"/>, may be inserted between Teaching and Recall
/// (Hypothesis → Teaching → Examples → Recall → Production → Assessment)
/// ONLY for capabilities with
/// <see cref="HumanOS.Models.Capabilities.Capability.IncludeExamplesStep"/> =
/// true — every other capability's blueprints are entirely unaffected. This
/// enum's numeric values are NOT used directly as SortOrder anymore (see
/// NodeExperienceBlueprintPersistenceService's canonical order arrays) —
/// <see cref="DeepDive"/> was deliberately added as a new trailing value
/// (=5) rather than inserted at position 2, so it never renumbers/corrupts
/// the already-persisted SortOrder of any existing step row.
/// </summary>
public enum ExperienceStepType
{
    /// <summary>Built from Interpretation + Illustrations. Creates an initial prediction/hypothesis before teaching.</summary>
    Hypothesis = 0,

    /// <summary>Built from AcademicDefinition + Interpretation + Examples + Illustrations. Explains the concept/skill.</summary>
    Teaching = 1,

    /// <summary>Built from AcademicDefinition + Interpretation. Active-recall retrieval question.</summary>
    Recall = 2,

    /// <summary>Built from Applications. Authentic production task.</summary>
    Production = 3,

    /// <summary>Built from success criteria (explicit or auto-generated). Verifies mastery.</summary>
    Assessment = 4,

    /// <summary>
    /// OPTIONAL step (2026-07-29 — formal K-12/school-track capabilities),
    /// present ONLY when the owning Capability has IncludeExamplesStep=true.
    /// Shows 3+ short, concrete "example cards" (Title + HTML Text + an
    /// optional cheap Emoji illustration — see <see cref="ExampleCard"/>),
    /// positioned between Teaching and Recall. Built from the node's own
    /// Examples/AcademicDefinition — never invents new facts.
    /// </summary>
    DeepDive = 5
}
