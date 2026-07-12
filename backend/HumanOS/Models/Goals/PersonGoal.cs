using HumanOS.Models.People;

namespace HumanOS.Models.Goals;

public sealed class PersonGoal
{
    public Guid PersonGoalId { get; set; }

    public Guid PersonId { get; set; }

    public Guid GoalId { get; set; }

    public string Status { get; set; } = "Active";

    public decimal ProgressPercentage { get; set; } = 0;

    public DateTime? TargetDate { get; set; }

    public DateTime StartedDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Person Person { get; set; } = null!;

    public Goal Goal { get; set; } = null!;
}
