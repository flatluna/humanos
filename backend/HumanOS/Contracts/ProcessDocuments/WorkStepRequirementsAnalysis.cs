using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>
/// Output schema for the analysis of one extracted work step,
/// identifying what an employee must be able to do to perform the step successfully.
/// </summary>
public sealed class WorkStepRequirementsAnalysisResult
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("stepId")]
    public string StepId { get; set; } = string.Empty;

    [JsonPropertyName("stepNumber")]
    public string StepNumber { get; set; } = string.Empty;

    [JsonPropertyName("workStep")]
    public string WorkStep { get; set; } = string.Empty;

    [JsonPropertyName("employeeMustBeAbleTo")]
    public List<string> EmployeeMustBeAbleTo { get; set; } = [];

    [JsonPropertyName("requiredSkillAreas")]
    public RequiredSkillAreas RequiredSkillAreas { get; set; } = new();

    [JsonPropertyName("requiredTools")]
    public List<RequiredToolItem> RequiredTools { get; set; } = [];

    [JsonPropertyName("completionEvidence")]
    public List<string> CompletionEvidence { get; set; } = [];

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("sourcePages")]
    public List<int> SourcePages { get; set; } = [];
}

public sealed class RequiredSkillAreas
{
    [JsonPropertyName("softSkills")]
    public List<SkillItem> SoftSkills { get; set; } = [];

    [JsonPropertyName("businessSkills")]
    public List<SkillItem> BusinessSkills { get; set; } = [];

    [JsonPropertyName("technicalSkills")]
    public List<SkillItem> TechnicalSkills { get; set; } = [];

    [JsonPropertyName("departmentProcesses")]
    public List<SkillItem> DepartmentProcesses { get; set; } = [];

    [JsonPropertyName("hrProcesses")]
    public List<SkillItem> HrProcesses { get; set; } = [];

    [JsonPropertyName("aiSkills")]
    public List<SkillItem> AiSkills { get; set; } = [];
}

public sealed class SkillItem
{
    /// <summary>
    /// Deterministic canonical ID, e.g., "skill-soft-stakeholder-management" or "skill-job-prompt-engineering".
    /// Reused across all steps in the process to consolidate requirements.
    /// </summary>
    [JsonPropertyName("skillId")]
    public string SkillId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// "soft-skills" | "job-skills" | "department-processes" | "hr-processes"
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = "job-skills";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("isAiSkill")]
    public bool IsAiSkill { get; set; }

    [JsonPropertyName("isInCatalog")]
    public bool IsInCatalog { get; set; }
}

public sealed class RequiredToolItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("useInStep")]
    public string UseInStep { get; set; } = string.Empty;
}

/// <summary>
/// Observability execution metrics for the work step requirements analysis.
/// </summary>
public sealed class WorkStepAnalysisObservability
{
    public string DocumentId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string ModelDeployment { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public int WarningsCount { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
