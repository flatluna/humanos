namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// The five pedagogical step types of the "Memory Paradox" pattern — the
/// fixed, official teaching sequence every <see cref="NodeExperienceBlueprint"/>
/// must follow:
///
///   Hypothesis → Teaching → Recall → Production → Assessment
///
/// The order is never changed and no new step types are added — this enum's
/// numeric values double as the canonical SortOrder for a blueprint's steps.
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
    Assessment = 4
}
