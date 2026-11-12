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

    public string AssignmentScope { get; set; } = null!;

    public IReadOnlyList<Guid> ApplicableDepartmentIds { get; set; } = [];

    public bool IsActive { get; set; }

    public string? DocumentStoragePath { get; set; }

    public string? DocumentFileName { get; set; }

    public string? DocumentUrl { get; set; }

    public IReadOnlyList<SkillJobRoleLinkResponse> JobRoles { get; set; } = [];

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /// <summary>Set when an active Capability already satisfies this skill
    /// (matched the same way as PersonCareerPlanService.AutoMatchCapabilitiesAsync
    /// — see SkillService.GetMatchedCapabilitiesAsync) — null means no course
    /// exists yet for this skill. Powers the "Curso creado" vs "Generar curso"
    /// indicator on the enterprise console's Skill catalog page.</summary>
    public Guid? MatchedCapabilityId { get; set; }

    public string? MatchedCapabilityName { get; set; }

    /// <summary>"structured-learning" or "graph" — indicates which Studio
    /// player/detail route to open.</summary>
    public string? MatchedCourseType { get; set; }

    public IReadOnlyList<SkillMatchedCourseResponse> MatchedCourses { get; set; } = [];

    /// <summary>Set only for skills created from a real internet-sourced course
    /// recommendation (Step 4 "Link to Process" on an internet gap) — the
    /// original course title, provider and URL preserved for reference.</summary>
    public string? SourceCourseTitle { get; set; }

    public string? SourceCourseProvider { get; set; }

    public string? SourceCourseUrl { get; set; }

    /// <summary>"Index extracted from the course page" outline modules captured
    /// at save time, if any were available (see Skill.SourceOutlineJson).</summary>
    public IReadOnlyList<HumanOS.Contracts.ProcessDocuments.CourseOutlineModule>? SourceOutlineModules { get; set; }

    public string? SourceOutlineSource { get; set; }

    /// <summary>Set only when this Skill represents an ENTIRE business process saved as a
    /// "department-processes" capability (Step 4 "Guardar en Catálogo de Capabilities"), not an
    /// individual skill gap — points back at the ProcessDocument so Studio can read its original
    /// PDF + chapter/step outline.</summary>
    public Guid? SourceProcessDocumentId { get; set; }

    /// <summary>The department that authored the source process (e.g. "Finanzas"), resolved from
    /// ProcessDocument.DepartmentId — only set alongside <see cref="SourceProcessDocumentId"/>.</summary>
    public string? SourceProcessDepartmentName { get; set; }
}

public sealed class SkillMatchedCourseResponse
{
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public string CourseType { get; set; } = null!;
    public string LanguageCode { get; set; } = "es";
}

/// <summary>One Job Role this Skill is currently linked to — enough to
/// render a chip/row without a second round-trip.</summary>
public sealed class SkillJobRoleLinkResponse
{
    public Guid JobRoleId { get; set; }

    public string JobRoleTitle { get; set; } = null!;

    public string DepartmentName { get; set; } = null!;

    /// <summary>"L100" | "L200" | "L300", or null — content depth this
    /// specific role requires for this Skill (see JobRoleSkill.RequiredLevel).</summary>
    public string? RequiredLevel { get; set; }
}

/// <summary>Lightweight projection of <see cref="SkillResponse"/> for
/// callers that only need to let someone PICK a skill by name (e.g.
/// Capability Studio's "target skill" combobox) — no Description,
/// timestamps, or per-Job-Role title/department detail, just enough to
/// render "{Name} ({JobRoleCount})". Returned by GET
/// /tenants/{tenantId}/skills when called with ?summary=true (see
/// GetSkillsFunction), instead of the full <see cref="SkillResponse"/>
/// the enterprise console's Skill catalog management page needs.</summary>
public sealed class SkillSummaryResponse
{
    public Guid SkillId { get; set; }

    public string Name { get; set; } = null!;

    public int JobRoleCount { get; set; }
}

/// <summary>One business process this Skill is linked to via a saved
/// ProcessSkillRequirement (Step 4 "Link to Process" of the whole-process
/// Learning Recommendation wizard) — the reverse of ProcessSkillRequirement's
/// MatchedSkillId FK. Powers the Skill catalog's "Used in these processes"
/// panel so HR can see the impact of editing/deleting a catalog skill.</summary>
public sealed class SkillLinkedProcessResponse
{
    public Guid ProcessDocumentId { get; set; }

    public Guid ProcessSkillRequirementId { get; set; }

    public string ProcessTitle { get; set; } = null!;

    public bool IsRequired { get; set; }

    public bool AppliesToAllDepartments { get; set; }

    public int DepartmentCount { get; set; }

    public int JobRoleCount { get; set; }
}
