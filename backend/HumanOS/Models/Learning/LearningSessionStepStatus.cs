namespace HumanOS.Models.Learning;

/// <summary>
/// Lifecycle status of a <see cref="LearningSessionStep"/> — the real
/// execution of one Memory Paradox step (Hypothesis/Teaching/Recall/
/// Production/Assessment) for a student.
/// </summary>
public enum LearningSessionStepStatus
{
    NotStarted,
    Active,
    Completed
}
