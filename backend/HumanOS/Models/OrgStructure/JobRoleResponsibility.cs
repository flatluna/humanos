namespace HumanOS.Models.OrgStructure;

/// <summary>
/// Many-to-many junction: which <see cref="JobRole"/>s include a given <see cref="Responsibility"/>.
/// </summary>
public sealed class JobRoleResponsibility
{
    public Guid JobRoleId { get; set; }

    public JobRole JobRole { get; set; } = null!;

    public Guid ResponsibilityId { get; set; }

    public Responsibility Responsibility { get; set; } = null!;

    public DateTime CreatedDate { get; set; }
}
