namespace HumanOS.Models.Learning;

/// <summary>Status of one <see cref="AssessmentRound"/> attempt cycle.</summary>
public enum AssessmentRoundStatus
{
    /// <summary>Between 1 and 5 questions have been asked/answered so far.</summary>
    InProgress = 0,

    /// <summary>All 5 questions answered, FinalScore &gt;= 80.</summary>
    Passed = 1,

    /// <summary>All 5 questions answered, FinalScore &lt; 80. A new round is auto-started.</summary>
    Failed = 2
}
