using HumanOS.Models.Capabilities;

namespace HumanOS.Models.Assessments;

public sealed class Assessment
{
    public Guid AssessmentId { get; set; }

    public Guid CapabilityId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string AssessmentType { get; set; } = null!;

    public decimal PassingScore { get; set; } = 70;

    public decimal MaxScore { get; set; } = 100;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public Capability Capability { get; set; } = null!;

    public ICollection<AssessmentAttempt> Attempts { get; set; } = [];
}
