namespace HumanOS.Models.OrgStructure;

/// <summary>
/// Many-to-many junction: which <see cref="JobRole"/>s use a given <see cref="Tool"/>.
/// </summary>
public sealed class JobRoleTool
{
    public Guid JobRoleId { get; set; }

    public JobRole JobRole { get; set; } = null!;

    public Guid ToolId { get; set; }

    public Tool Tool { get; set; } = null!;

    public DateTime CreatedDate { get; set; }
}
