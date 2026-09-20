namespace HumanOS.Contracts.OrgStructure;

/// <summary>Create/update body for the tenant's Skill catalog.</summary>
public sealed class SaveSkillRequest
{
    /// <summary>One of "job-skills" | "soft-skills" |
    /// "department-processes" | "hr-processes".</summary>
    public string Category { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? AssignmentScope { get; set; }

    public List<Guid>? ApplicableDepartmentIds { get; set; }

    /// <summary>Every Job Role this Skill should be linked to, each with its
    /// own optional content-depth preference — replaces the full set on
    /// update (any role not listed here gets unlinked). Prefer this over
    /// <see cref="JobRoleIds"/> going forward.</summary>
    public List<SkillJobRoleLinkRequest>? JobRoleLinks { get; set; }

    /// <summary>Legacy shape (plain IDs, no level) — still accepted for
    /// backward compatibility; ignored when <see cref="JobRoleLinks"/> is set.</summary>
    public List<Guid>? JobRoleIds { get; set; }
}

/// <summary>One Job Role to link a Skill to, plus the content depth this
/// role requires for it ("L100" | "L200" | "L300", or null for no
/// preference) — e.g. the same "Python Programming" Skill can be L100 for
/// a Jr role and L300 for a Sr role.</summary>
public sealed class SkillJobRoleLinkRequest
{
    public Guid JobRoleId { get; set; }

    public string? RequiredLevel { get; set; }
}
