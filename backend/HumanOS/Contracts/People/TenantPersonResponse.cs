namespace HumanOS.Contracts.People;

/// <summary>One row in a Tenant's people directory (GET
/// /tenants/{tenantId}/people) — powers the org-structure admin console's
/// "assign department/role/manager" pickers and person list.</summary>
public sealed class TenantPersonResponse
{
    public Guid PersonId { get; set; }

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public string? LinkedInUrl { get; set; }

    public Guid? DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    public Guid? JobRoleId { get; set; }

    public string? JobRoleTitle { get; set; }

    public Guid? ManagerId { get; set; }

    public string? ManagerDisplayName { get; set; }

    /// <summary>String name of PersonOrgRole ("Employee"/"Manager"/"HRAdmin").</summary>
    public string OrgRole { get; set; } = "Employee";

    public bool IsActive { get; set; }
}
