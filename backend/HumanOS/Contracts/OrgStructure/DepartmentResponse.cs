namespace HumanOS.Contracts.OrgStructure;

/// <summary>Response for GET /tenants/{tenantId}/departments and the
/// create/update department endpoints.</summary>
public sealed class DepartmentResponse
{
    public Guid DepartmentId { get; set; }

    public Guid TenantId { get; set; }

    public Guid? ParentDepartmentId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public int JobRoleCount { get; set; }

    public int PersonCount { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
