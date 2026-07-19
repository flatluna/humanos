namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// Which part of the blueprint (or its grounding source) a
/// <see cref="BlueprintValidationIssue"/> refers to. The first 5 values
/// mirror <see cref="ExperienceStepType"/> 1:1; the last 2 refer to
/// cross-cutting concerns that are not a single step.
/// </summary>
public enum BlueprintValidationArea
{
    Hypothesis,
    Teaching,
    Recall,
    Production,
    Assessment,

    /// <summary>Reuse of the node's existing illustrations across steps.</summary>
    Illustration,

    /// <summary>Traceability of the blueprint's content back to the node's own fields.</summary>
    References
}
