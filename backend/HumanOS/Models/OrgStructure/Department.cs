namespace HumanOS.Models.OrgStructure;

using HumanOS.Models.Tenancy;

/// <summary>
/// A node in a Tenant's organizational chart. Deliberately modeled as a
/// single self-referencing tree (via <see cref="ParentDepartmentId"/>)
/// rather than fixed "Área/Departamento/Equipo" levels — organizations
/// have very different depths, so any level of nesting (or none at all)
/// is supported by the same table. <see cref="JobRole"/>s and
/// <see cref="People.Person"/>s attach directly to whichever Department
/// node makes sense for that org (a flat org can just use one level).
/// </summary>
public sealed class Department
{
    public Guid DepartmentId { get; set; }

    public Guid TenantId { get; set; }

    public Guid? ParentDepartmentId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Tenant Tenant { get; set; } = null!;

    public Department? ParentDepartment { get; set; }

    public ICollection<Department> ChildDepartments { get; set; } = [];

    public ICollection<JobRole> JobRoles { get; set; } = [];
}
