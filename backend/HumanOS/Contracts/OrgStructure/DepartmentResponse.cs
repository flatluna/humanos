namespace HumanOS.Contracts.OrgStructure;

/// <summary>Response for GET /tenants/{tenantId}/departments and the
/// create/update department endpoints.</summary>
public sealed class DepartmentResponse
{
    public Guid DepartmentId { get; set; }

    public Guid TenantId { get; set; }

    public Guid? ParentDepartmentId { get; set; }

    public string Name { get; set; } = null!;

    public string? Code { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public string? ContactEmail { get; set; }

    public string? Location { get; set; }

    public string? CostCenter { get; set; }

    public string? MissionStatement { get; set; }

    public Guid? LeaderPersonId { get; set; }

    public string? LeaderDisplayName { get; set; }

    public bool IsActive { get; set; }

    public int JobRoleCount { get; set; }

    public int PersonCount { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
