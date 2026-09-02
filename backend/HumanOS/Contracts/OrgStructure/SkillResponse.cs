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
}

/// <summary>One Job Role this Skill is currently linked to — enough to
/// render a chip/row without a second round-trip.</summary>
public sealed class SkillJobRoleLinkResponse
{
    public Guid JobRoleId { get; set; }

    public string JobRoleTitle { get; set; } = null!;

    public string DepartmentName { get; set; } = null!;
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
