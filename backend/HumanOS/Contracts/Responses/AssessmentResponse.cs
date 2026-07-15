namespace HumanOS.Contracts.Responses;

public sealed class AssessmentResponse
{
    public Guid AssessmentId { get; set; }

    public Guid CapabilityId { get; set; }

    public string CapabilityCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string AssessmentType { get; set; } = null!;

    public decimal PassingScore { get; set; }

    public decimal MaxScore { get; set; }

    public bool IsActive { get; set; }
}

public sealed class AssessmentAttemptResponse
{
    public Guid AssessmentAttemptId { get; set; }

    public Guid AssessmentId { get; set; }

    public Guid PersonId { get; set; }

    public decimal? Score { get; set; }

    public int AssistanceLevel { get; set; }

    public DateTime StartedDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public DateTime CreatedDate { get; set; }
}
