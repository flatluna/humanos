namespace HumanOS.Models.People;

/// <summary>
/// An AI-generated career development plan for one Person — compares the
/// person's current skills (from <see cref="PersonResume.SkillsJson"/>)
/// against their assigned <see cref="HumanOS.Models.OrgStructure.JobRole"/>'s
/// required skills (TechnicalSkillsJson/SoftSkillsJson), surfaces the gap,
/// and recommends real, existing catalog Programs to close it — see
/// <see cref="HumanOS.Agents.CareerDevelopmentPlanAgent"/>.
///
/// Regenerated on demand ("Generar plan con IA" in the enterprise
/// console's "Rutas de Carrera" tab) — this row always holds the LATEST
/// generated plan, not a history. One optional row per Person, created
/// lazily on first generation, same "created lazily" rationale as
/// <see cref="PersonResume"/>/<see cref="PersonProfile"/>.
/// </summary>
public sealed class PersonCareerPlan
{
    public Guid PersonCareerPlanId { get; set; }

    public Guid PersonId { get; set; }

    /// <summary>The JobRole this plan was generated against — a snapshot
    /// reference, not necessarily still the person's CURRENT JobRoleId if
    /// they were reassigned after this plan was generated (the UI should
    /// prompt regeneration when they differ).</summary>
    public Guid JobRoleId { get; set; }

    /// <summary>JSON string[] — the required skills (technical + soft,
    /// combined) the person's current skills didn't cover, as identified
    /// by the agent.</summary>
    public string GapSkillsJson { get; set; } = "[]";

    /// <summary>JSON-serialized list of
    /// <c>HumanOS.Contracts.People.CareerPlanRoadmapStepDto</c> — the
    /// ordered training roadmap covering the required TECHNICAL skill
    /// gaps only. Steps may reference a real catalog Program
    /// (MatchedProgramId) or, when none exists yet, just a proposed
    /// title/description for HR to create later.</summary>
    public string TechnicalRoadmapJson { get; set; } = "[]";

    /// <summary>Same shape as <see cref="TechnicalRoadmapJson"/> but for
    /// the required SOFT skill gaps only.</summary>
    public string SoftSkillsRoadmapJson { get; set; } = "[]";

    /// <summary>Same shape as <see cref="TechnicalRoadmapJson"/> but for
    /// the required DEPARTMENT PROCESS gaps only (JobRole.DepartmentProcessesJson).</summary>
    public string DepartmentProcessesRoadmapJson { get; set; } = "[]";

    /// <summary>Same shape as <see cref="TechnicalRoadmapJson"/> but for
    /// the required HR PROCESS gaps only (JobRole.HrProcessesJson).</summary>
    public string HrProcessesRoadmapJson { get; set; } = "[]";

    /// <summary>Short, personalized narrative explaining the overall plan
    /// (uses the person's name/role, same language as the role's data).</summary>
    public string? OverallRationale { get; set; }

    /// <summary>The Azure OpenAI deployment/model that generated this plan
    /// (e.g. "gpt-5-mini"), for audit purposes.</summary>
    public string? GeneratedModel { get; set; }

    public DateTime GeneratedDate { get; set; }

    /// <summary>True once HR has explicitly confirmed this AI-proposed
    /// plan as the employee's official training plan ("Aceptar plan" in
    /// the UI). Reset to false whenever the plan is regenerated, since a
    /// new proposal needs its own review/acceptance.</summary>
    public bool IsAccepted { get; set; }

    /// <summary>When <see cref="IsAccepted"/> was set to true. Null while
    /// the plan is still just an AI proposal awaiting HR review.</summary>
    public DateTime? AcceptedDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Person Person { get; set; } = null!;
}
