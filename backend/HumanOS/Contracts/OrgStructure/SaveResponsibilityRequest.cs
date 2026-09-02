namespace HumanOS.Contracts.OrgStructure;

/// <summary>Create/update body for the tenant's Key Responsibility catalog.</summary>
public sealed class SaveResponsibilityRequest
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public List<Guid>? JobRoleIds { get; set; }
}
