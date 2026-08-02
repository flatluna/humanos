namespace HumanOS.Contracts.OrgStructure;

/// <summary>Create/update body for the tenant's Skill catalog.</summary>
public sealed class SaveSkillRequest
{
    /// <summary>One of "job-skills" | "soft-skills" |
    /// "department-processes" | "hr-processes".</summary>
    public string Category { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>Every Job Role this Skill should be linked to — replaces
    /// the full set on update (any role not listed here gets unlinked).</summary>
    public List<Guid>? JobRoleIds { get; set; }
}
