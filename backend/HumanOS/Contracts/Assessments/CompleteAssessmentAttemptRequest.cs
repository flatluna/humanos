namespace HumanOS.Contracts.Assessments;

public sealed class CompleteAssessmentAttemptRequest
{
    public int? Score { get; set; }

    public int AssistanceLevel { get; set; }
}
