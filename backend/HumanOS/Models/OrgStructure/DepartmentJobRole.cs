namespace HumanOS.Models.OrgStructure;

/// <summary>
/// Many-to-many junction connecting a <see cref="Department"/> to a <see cref="JobRole"/>.
/// Allows one Job Role (position) to belong to multiple departments without duplication,
/// and allows detaching roles from a department without deleting the Job Role entity.
/// </summary>
public sealed class DepartmentJobRole
{
    public Guid DepartmentId { get; set; }

    public Guid JobRoleId { get; set; }

    public DateTime AssignedDate { get; set; }

    public Department Department { get; set; } = null!;

    public JobRole JobRole { get; set; } = null!;
}
