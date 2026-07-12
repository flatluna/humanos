using HumanOS.Models.People;

namespace HumanOS.Models.Assessments;

public sealed class AssessmentAttempt
{
    public Guid AssessmentAttemptId { get; set; }

    public Guid AssessmentId { get; set; }

    public Guid PersonId { get; set; }

    public decimal? Score { get; set; }

    public int AssistanceLevel { get; set; }

    public DateTime StartedDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public Assessment Assessment { get; set; } = null!;

    public Person Person { get; set; } = null!;
}
