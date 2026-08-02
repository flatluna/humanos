namespace HumanOS.Models.OrgStructure;

/// <summary>
/// A reusable, catalog-level training requirement ("skill", process, or
/// policy) that HR authors ONCE and can link to one or MANY
/// <see cref="JobRole"/>s at a time — e.g. "Prevención de Acoso Sexual
/// Laboral" (category "hr-processes") applies to every job role in the
/// company, not just one (2026-08-01 "cierra el ciclo" feature).
///
/// Distinct from JobRole's own *SkillsJson/*ProcessesJson free-text lists
/// (kept as a convenient per-role authoring shortcut): editing a role's
/// list still works exactly as before and automatically finds-or-creates
/// a matching Skill row + link here (see
/// JobRoleService.ReconcileSkillCatalogAsync). Editing/deleting a Skill
/// from the new catalog page, or linking it to additional roles in bulk,
/// propagates everywhere it's linked without HR re-typing anything.
/// </summary>
public sealed class Skill
{
    public Guid SkillId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>One of "job-skills" | "soft-skills" |
    /// "department-processes" | "hr-processes" — the same 4 career-plan
    /// tracks used throughout this feature (see
    /// PersonCareerPlanService.AutoMatchCapabilitiesAsync).</summary>
    public string Category { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public ICollection<JobRoleSkill> JobRoleSkills { get; set; } = new List<JobRoleSkill>();
}
