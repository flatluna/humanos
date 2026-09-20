namespace HumanOS.Models.OrgStructure;

/// <summary>Many-to-many join: which <see cref="JobRole"/>s require a
/// given <see cref="Skill"/>. See Skill's own doc comment for the
/// rationale (one skill/process/policy shared across many job roles).</summary>
public sealed class JobRoleSkill
{
    public Guid JobRoleId { get; set; }

    public JobRole JobRole { get; set; } = null!;

    public Guid SkillId { get; set; }

    public Skill Skill { get; set; } = null!;

    /// <summary>"L100" | "L200" | "L300", or null when no depth preference
    /// was set — the same Skill can require L100 for a Jr role and L300 for
    /// a Sr role, so the level lives on this link, not on Skill/JobRole.</summary>
    public string? RequiredLevel { get; set; }

    public DateTime CreatedDate { get; set; }
}
