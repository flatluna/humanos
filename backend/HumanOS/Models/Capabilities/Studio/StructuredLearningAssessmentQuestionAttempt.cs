namespace HumanOS.Models.Capabilities.Studio;

/// <summary>Mirrors <see cref="HumanOS.Models.Learning.AssessmentQuestionCorrectness"/>.</summary>
public enum StructuredLearningAssessmentAnswerCorrectness
{
    Pending,
    Correct,
    PartiallyCorrect,
    Incorrect
}

/// <summary>
/// One dynamically-generated question within a <see cref="StructuredLearningAssessmentRound"/>.
/// Never reused across rounds — see that class's doc comment. Mirrors
/// <see cref="HumanOS.Models.Learning.AssessmentQuestion"/>.
/// </summary>
public class StructuredLearningAssessmentQuestionAttempt
{
    public Guid StructuredLearningAssessmentQuestionAttemptId { get; set; } = Guid.NewGuid();

    public Guid StructuredLearningAssessmentRoundId { get; set; }

    public int QuestionIndex { get; set; }

    public string QuestionText { get; set; } = string.Empty;

    /// <summary>JSON-serialized list of options — only set for MultipleChoice questions.</summary>
    public string? OptionsJson { get; set; }

    /// <summary>Only set for MultipleChoice questions.</summary>
    public int? CorrectOptionIndex { get; set; }

    /// <summary>The student's answer — free text for OpenEnded, the
    /// selected option's index (as a string) for MultipleChoice. Null
    /// until answered.</summary>
    public string? StudentAnswer { get; set; }

    public StructuredLearningAssessmentAnswerCorrectness Correctness { get; set; } = StructuredLearningAssessmentAnswerCorrectness.Pending;

    /// <summary>Raw score 0-100 — for MultipleChoice this is decided
    /// deterministically in code (100 or 0); for OpenEnded it's proposed by
    /// <see cref="Agents.Studio.StructuredLearningAssessmentAgent"/>, never
    /// trusted directly for <see cref="Correctness"/> (always re-derived in
    /// code from fixed thresholds).</summary>
    public int? ScoreContribution { get; set; }

    public string? Feedback { get; set; }

    /// <summary>Short label of the misconception revealed, or null. Only
    /// meaningful for OpenEnded (MultipleChoice grading has nothing to
    /// observe beyond right/wrong).</summary>
    public string? ObservedError { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? AnsweredDate { get; set; }

    public StructuredLearningAssessmentRound Round { get; set; } = null!;
}
