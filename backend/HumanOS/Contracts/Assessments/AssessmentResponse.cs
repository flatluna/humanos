namespace HumanOS.Contracts.Assessments;

public sealed class AssessmentResponse
{
    public Guid AssessmentId { get; set; }

    public Guid CapabilityId { get; set; }

    public string CapabilityCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string AssessmentType { get; set; } = null!;

    public int PassingScore { get; set; }

    public int MaxScore { get; set; }

    public bool IsActive { get; set; }
}
