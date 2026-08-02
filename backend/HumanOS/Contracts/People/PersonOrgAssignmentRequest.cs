namespace HumanOS.Contracts.People;

/// <summary>Body for PUT /people/{personId}/org-assignment — an HRAdmin
/// assigning/changing a person's Department, JobRole, Manager, and/or
/// permission tier. Every field is a full replace (null clears the
/// assignment for Department/JobRole/Manager); <see cref="OrgRole"/> is
/// the string name of <see cref="HumanOS.Models.People.PersonOrgRole"/>
/// ("Employee"/"Manager"/"HRAdmin").</summary>
public sealed class PersonOrgAssignmentRequest
{
    public Guid? DepartmentId { get; set; }

    public Guid? JobRoleId { get; set; }

    public Guid? ManagerId { get; set; }

    public string? OrgRole { get; set; }
}
