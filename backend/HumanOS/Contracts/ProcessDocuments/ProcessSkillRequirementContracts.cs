using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>Request body for saving a Step 4 skill gap as a formal training requirement for the
/// process, scoped to specific Job Roles/Departments or the whole company.</summary>
public sealed class SaveProcessSkillRequirementRequest
{
    [JsonPropertyName("skillName")]
    public string SkillName { get; set; } = string.Empty;

    /// <summary>"soft-skills" | "technology-skills".</summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = "soft-skills";

    [JsonPropertyName("isAiSkill")]
    public bool IsAiSkill { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("appliesToAllDepartments")]
    public bool AppliesToAllDepartments { get; set; }

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; } = true;

    [JsonPropertyName("departmentIds")]
    public List<string> DepartmentIds { get; set; } = [];

    [JsonPropertyName("jobRoleIds")]
    public List<string> JobRoleIds { get; set; } = [];

    /// <summary>Case (a) — the gap was already matched to an existing dbo.Skill catalog entry
    /// (see <c>ProcessSkillGapItem.MatchedSkillId</c>). When set, NO new Skill row is created —
    /// this existing one is simply linked/assigned.</summary>
    [JsonPropertyName("matchedSkillId")]
    public string? MatchedSkillId { get; set; }

    /// <summary>Case (b) — the gap came from a real internet course (not in the catalog). When
    /// true, a NEW dbo.Skill row is created from the fields below before the assignment is saved.</summary>
    [JsonPropertyName("isNewFromInternet")]
    public bool IsNewFromInternet { get; set; }

    [JsonPropertyName("sourceCourseTitle")]
    public string? SourceCourseTitle { get; set; }

    [JsonPropertyName("sourceCourseProvider")]
    public string? SourceCourseProvider { get; set; }

    [JsonPropertyName("sourceCourseUrl")]
    public string? SourceCourseUrl { get; set; }

    /// <summary>The already-extracted/generated course outline (see GetCourseOutline), stored on
    /// the new Skill so Studio can later use it as a starting draft — never regenerated here.</summary>
    [JsonPropertyName("sourceOutlineModules")]
    public List<CourseOutlineModule>? SourceOutlineModules { get; set; }

    [JsonPropertyName("sourceOutlineSource")]
    public string? SourceOutlineSource { get; set; }
}

public sealed class ProcessSkillRequirementResponse
{
    [JsonPropertyName("processSkillRequirementId")]
    public Guid ProcessSkillRequirementId { get; set; }

    [JsonPropertyName("processDocumentId")]
    public Guid ProcessDocumentId { get; set; }

    [JsonPropertyName("skillName")]
    public string SkillName { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("isAiSkill")]
    public bool IsAiSkill { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("appliesToAllDepartments")]
    public bool AppliesToAllDepartments { get; set; }

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; }

    [JsonPropertyName("departmentIds")]
    public List<string> DepartmentIds { get; set; } = [];

    [JsonPropertyName("jobRoleIds")]
    public List<string> JobRoleIds { get; set; } = [];

    [JsonPropertyName("matchedSkillId")]
    public string? MatchedSkillId { get; set; }
}

/// <summary>Request body for POST /process-documents/{documentId}/save-as-capability — saves the
/// ENTIRE process (not an individual skill gap) as a "department-processes" Capability Catalog
/// Skill, so it shows up in /skills next to the individual skill gaps and can be opened in Studio
/// to generate its course. The process's original PDF + chapter/step outline are reused as-is.</summary>
public sealed class SaveProcessAsCapabilityRequest
{
    /// <summary>The department that authored/owns this process (e.g. HR, Finance) — required even
    /// when the process applies to many departments; this only records who wrote it, not who must
    /// follow it (see ProcessDocumentRecord.ScopedDepartments/AppliesToAllDepartments for that).</summary>
    [JsonPropertyName("departmentId")]
    public string DepartmentId { get; set; } = string.Empty;
}

public sealed class SaveProcessAsCapabilityResponse
{
    [JsonPropertyName("skillId")]
    public Guid SkillId { get; set; }

    [JsonPropertyName("alreadyExisted")]
    public bool AlreadyExisted { get; set; }
}
