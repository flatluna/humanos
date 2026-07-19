namespace HumanOS.Models.Learning;

/// <summary>
/// Lifecycle status of a <see cref="LearningSessionNode"/> — a student's
/// progress through one CapabilityGraphNode's NodeExperienceBlueprint.
/// </summary>
public enum LearningSessionNodeStatus
{
    NotStarted,
    Active,
    Completed,
    Failed
}
