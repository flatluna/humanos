namespace HumanOS.Contracts.OrgStructure;

/// <summary>Response for GET/POST/PUT on the tenant's Certification catalog.</summary>
public sealed class CertificationResponse
{
    public Guid CertificationId { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    public string? Issuer { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public IReadOnlyList<CertificationJobRoleLinkResponse> JobRoles { get; set; } = [];

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}

public sealed class CertificationJobRoleLinkResponse
{
    public Guid JobRoleId { get; set; }

    public string JobRoleTitle { get; set; } = null!;

    public string DepartmentName { get; set; } = null!;
}
