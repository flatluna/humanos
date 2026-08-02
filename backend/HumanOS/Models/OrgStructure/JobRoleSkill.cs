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

    public DateTime CreatedDate { get; set; }
}
