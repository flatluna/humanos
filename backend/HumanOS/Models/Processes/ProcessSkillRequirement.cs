namespace HumanOS.Models.Processes;

/// <summary>
/// Persists a skill gap identified in the whole-process Learning Recommendation (Step 4 of the
/// wizard) as an actual training requirement for a given process, scoped to specific Job Roles
/// and/or Departments, or to the whole company. Distinct from <see cref="ProcessStepSkillRecord"/>
/// (the per-step Training & Skill Matrix, Step 5): this table is the process-wide, human-approved
/// decision of WHO must have this skill and whether it is mandatory.
/// </summary>
public sealed class ProcessSkillRequirement
{
    public Guid ProcessSkillRequirementId { get; set; }

    public Guid ProcessDocumentId { get; set; }

    public ProcessDocumentRecord? ProcessDocument { get; set; }

    public string SkillName { get; set; } = null!;

    /// <summary>"soft-skills" | "technology-skills" (same categories used by the
    /// whole-process learning recommendation).</summary>
    public string Category { get; set; } = "soft-skills";

    public bool IsAiSkill { get; set; }

    /// <summary>Why this skill is needed (copied from the AI recommendation's reasoning,
    /// editable by HR).</summary>
    public string? Reason { get; set; }

    /// <summary>When true, applies to every Department/Job Role in the tenant and
    /// <see cref="ScopedDepartments"/>/<see cref="ScopedJobRoles"/> are ignored.</summary>
    public bool AppliesToAllDepartments { get; set; }

    /// <summary>Whether this skill is mandatory ("requerido") or just recommended
    /// ("no requerido") for the selected scope.</summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>Links this requirement to the dbo.Skill catalog row it came from — either an
    /// existing catalog skill (the LLM matched the gap to it, nothing new was created) or a new
    /// row created at save time from a real internet course (see
    /// <see cref="HumanOS.Models.OrgStructure.Skill.SourceCourseTitle"/> and related fields).
    /// Null only for legacy requirements saved before this link existed.</summary>
    public Guid? MatchedSkillId { get; set; }

    public HumanOS.Models.OrgStructure.Skill? MatchedSkill { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    public List<ProcessSkillRequirementDepartment> ScopedDepartments { get; set; } = [];

    public List<ProcessSkillRequirementJobRole> ScopedJobRoles { get; set; } = [];
}
