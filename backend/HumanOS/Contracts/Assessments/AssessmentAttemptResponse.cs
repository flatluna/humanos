namespace HumanOS.Contracts.Assessments;

public sealed class AssessmentAttemptResponse
{
    public Guid AssessmentAttemptId { get; set; }

    public Guid AssessmentId { get; set; }

    public Guid PersonId { get; set; }

    public string AssessmentName { get; set; } = null!;

    public string CapabilityCode { get; set; } = null!;

    public int? Score { get; set; }

    public int PassingScore { get; set; }

    public bool Passed { get; set; }

    public int AssistanceLevel { get; set; }

    public DateTime StartedDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public DateTime CreatedDate { get; set; }
}
