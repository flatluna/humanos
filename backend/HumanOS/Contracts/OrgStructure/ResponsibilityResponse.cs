namespace HumanOS.Contracts.OrgStructure;

/// <summary>Response for GET/POST/PUT on the tenant's Key Responsibility catalog.</summary>
public sealed class ResponsibilityResponse
{
    public Guid ResponsibilityId { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public IReadOnlyList<ResponsibilityJobRoleLinkResponse> JobRoles { get; set; } = [];

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}

public sealed class ResponsibilityJobRoleLinkResponse
{
    public Guid JobRoleId { get; set; }

    public string JobRoleTitle { get; set; } = null!;

    public string DepartmentName { get; set; } = null!;
}
