namespace HumanOS.Contracts.People;

/// <summary>Body for PUT /people/{personId}/org-assignment — an HRAdmin
/// assigning/changing a person's Department, JobRole, Manager, permission tier,
/// and contact/profile details (email, display name, phone, address, LinkedIn).</summary>
public sealed class PersonOrgAssignmentRequest
{
    public Guid? DepartmentId { get; set; }

    public Guid? JobRoleId { get; set; }

    public Guid? ManagerId { get; set; }

    public string? OrgRole { get; set; }

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public string? LinkedInUrl { get; set; }
}
