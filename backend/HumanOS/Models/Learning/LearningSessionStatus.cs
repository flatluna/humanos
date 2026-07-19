namespace HumanOS.Models.Learning;

/// <summary>
/// Lifecycle status of a <see cref="LearningSession"/> — a student's overall
/// attempt at learning one Capability through its CapabilityGraph.
/// </summary>
public enum LearningSessionStatus
{
    NotStarted,
    Active,
    Completed,
    Abandoned
}
