namespace HumanOS.Contracts.OrgStructure;

/// <summary>Response for GET/POST/PUT on the tenant's Skill catalog — a
/// reusable training requirement that can be linked to one or many Job
/// Roles at once (see Models/OrgStructure/Skill.cs).</summary>
public sealed class SkillResponse
{
    public Guid SkillId { get; set; }

    public Guid TenantId { get; set; }

    public string Category { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public IReadOnlyList<SkillJobRoleLinkResponse> JobRoles { get; set; } = [];

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}

/// <summary>One Job Role this Skill is currently linked to — enough to
/// render a chip/row without a second round-trip.</summary>
public sealed class SkillJobRoleLinkResponse
{
    public Guid JobRoleId { get; set; }

    public string JobRoleTitle { get; set; } = null!;

    public string DepartmentName { get; set; } = null!;
}
