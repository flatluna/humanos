namespace HumanOS.Contracts.OrgStructure;

/// <summary>Create/update body for the tenant's Certification catalog.</summary>
public sealed class SaveCertificationRequest
{
    public string Name { get; set; } = null!;

    public string? Issuer { get; set; }

    public string? Description { get; set; }

    public List<Guid>? JobRoleIds { get; set; }
}
