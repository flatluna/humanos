namespace HumanOS.Models.OrgStructure;

/// <summary>
/// Many-to-many junction: which <see cref="JobRole"/>s include a given <see cref="Activity"/>.
/// </summary>
public sealed class JobRoleActivity
{
    public Guid JobRoleId { get; set; }

    public JobRole JobRole { get; set; } = null!;

    public Guid ActivityId { get; set; }

    public Activity Activity { get; set; } = null!;

    public DateTime CreatedDate { get; set; }
}
