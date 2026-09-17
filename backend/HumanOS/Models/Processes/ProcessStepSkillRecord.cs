using HumanOS.Models.OrgStructure;

namespace HumanOS.Models.Processes;

/// <summary>
/// The core Process Training Matrix link:
/// Connects a Process Step with a Skill from the tenant's catalog and/or an active Studio Course.
/// </summary>
public sealed class ProcessStepSkillRecord
{
    public Guid ProcessStepSkillId { get; set; }

    public Guid ProcessStepId { get; set; }

    public ProcessStepRecord? ProcessStep { get; set; }

    /// <summary>
    /// Foreign key to dbo.Skill if registered in tenant catalog.
    /// </summary>
    public Guid? SkillId { get; set; }

    public Skill? Skill { get; set; }

    public string SkillName { get; set; } = null!;

    /// <summary>
    /// Canonical category: soft-skills, job-skills, department-processes, hr-processes.
    /// </summary>
    public string Category { get; set; } = "job-skills";

    /// <summary>
    /// Indicates whether this is a mandatory AI learning requirement.
    /// </summary>
    public bool IsAiSkill { get; set; }

    /// <summary>
    /// Criticality: "critical" or "supporting".
    /// </summary>
    public string Criticality { get; set; } = "critical";

    /// <summary>
    /// Pedagogical reasoning for why this step requires this skill/course.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Coverage status: "covered", "in_catalog_pending_course", or "new_skill_required".
    /// </summary>
    public string CoverageStatus { get; set; } = "new_skill_required";

    /// <summary>
    /// Foreign key to matched course in dbo.StructuredLearningDocument or dbo.Capability.
    /// </summary>
    public Guid? MatchedCourseId { get; set; }

    public string? MatchedCourseName { get; set; }
}
