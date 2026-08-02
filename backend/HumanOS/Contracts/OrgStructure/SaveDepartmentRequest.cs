namespace HumanOS.Contracts.OrgStructure;

/// <summary>Body for POST /tenants/{tenantId}/departments and
/// PUT /departments/{departmentId}.</summary>
public sealed class SaveDepartmentRequest
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>Null for a top-level Department (Área). When set, must
    /// belong to the SAME tenant.</summary>
    public Guid? ParentDepartmentId { get; set; }
}
