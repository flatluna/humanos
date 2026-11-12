using System.ComponentModel.DataAnnotations.Schema;

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

    /// <summary>How this skill is assigned in the employee learning journey.
    /// Null is retained for legacy rows and resolved by SkillService.</summary>
    public string? AssignmentScope { get; set; }

    /// <summary>Department IDs stored as references only; department master
    /// data remains in Department.</summary>
    [NotMapped]
    public string? ApplicableDepartmentIdsJson { get; set; }

    public bool IsActive { get; set; } = true;

    public string? DocumentStoragePath { get; set; }

    public string? DocumentFileName { get; set; }

    /// <summary>When this Skill was auto-created from a real internet course found during the
    /// Step 4 Process Learning Wizard flow (instead of authored by HR), these fields keep the
    /// provenance so Studio can later generate the actual Capability/course content from it.
    /// Null for HR-authored catalog skills.</summary>
    public string? SourceCourseTitle { get; set; }

    public string? SourceCourseProvider { get; set; }

    public string? SourceCourseUrl { get; set; }

    /// <summary>Serialized <c>CourseOutlineResult.Modules</c> JSON (title + lessons per module),
    /// extracted/generated at save time so it isn't lost before Studio builds the course.</summary>
    public string? SourceOutlineJson { get; set; }

    /// <summary>"scraped" | "ai-suggested" — matches <c>CourseOutlineResult.Source</c>.</summary>
    public string? SourceOutlineSource { get; set; }

    /// <summary>Set when this Skill represents an entire business process saved as a
    /// Department/HR Process capability from Step 4 ("Guardar en Catálogo de Capabilities")
    /// instead of an individual skill gap — points back at the full ProcessDocument so Studio
    /// can read its original PDF + chapter/step outline to draft the course. Null for all other
    /// skills (individual gaps, HR-authored catalog entries, etc.).</summary>
    public Guid? SourceProcessDocumentId { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public ICollection<JobRoleSkill> JobRoleSkills { get; set; } = new List<JobRoleSkill>();
}
