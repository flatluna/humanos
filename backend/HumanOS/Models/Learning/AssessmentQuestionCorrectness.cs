namespace HumanOS.Models.Learning;

/// <summary>
/// Correctness verdict for one <see cref="AssessmentQuestion"/>. ALWAYS
/// derived deterministically in code from the agent's raw 0-100
/// ScoreContribution (fixed thresholds) — never trusted directly from the
/// LLM, same "LLM proposes, code decides" rule as
/// <see cref="LearningAssessmentResult.Passed"/>.
/// </summary>
public enum AssessmentQuestionCorrectness
{
    /// <summary>Not answered/graded yet.</summary>
    Pending = 0,
    Correct = 1,
    PartiallyCorrect = 2,
    Incorrect = 3
}
