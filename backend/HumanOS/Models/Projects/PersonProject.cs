using HumanOS.Models.People;

namespace HumanOS.Models.Projects;

public sealed class PersonProject
{
    public Guid PersonProjectId { get; set; }

    public Guid PersonId { get; set; }

    public Guid ProjectId { get; set; }

    public string Status { get; set; } = "NotStarted";

    public decimal ProgressPercentage { get; set; }

    public DateTime? StartedDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public Person Person { get; set; } = null!;

    public Project Project { get; set; } = null!;
}
