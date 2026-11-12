using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

public sealed class SaveProcessAnalysisRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("departmentId")]
    public Guid? DepartmentId { get; set; }

    [JsonPropertyName("jobRoleId")]
    public Guid? JobRoleId { get; set; }

    /// <summary>When true, the process applies to the whole organization —
    /// <see cref="DepartmentIds"/>/<see cref="JobRoleIds"/> are ignored.
    /// Normally sourced from <c>Structure.AppliesToAllDepartments</c> but can be
    /// overridden explicitly here.</summary>
    [JsonPropertyName("appliesToAllDepartments")]
    public bool? AppliesToAllDepartments { get; set; }

    /// <summary>Departments this process must be followed by. Normally sourced
    /// from <c>Structure.DepartmentIds</c> but can be overridden explicitly here.</summary>
    [JsonPropertyName("departmentIds")]
    public List<Guid>? DepartmentIds { get; set; }

    /// <summary>Job Roles this process must be followed by. Normally sourced
    /// from <c>Structure.JobRoleIds</c> but can be overridden explicitly here.</summary>
    [JsonPropertyName("jobRoleIds")]
    public List<Guid>? JobRoleIds { get; set; }

    [JsonPropertyName("structure")]
    public ProcessDocumentStructure? Structure { get; set; }

    [JsonPropertyName("stepCoverages")]
    public Dictionary<string, LearningContentCoverageResponse>? StepCoverages { get; set; }

    [JsonPropertyName("stepRequirements")]
    public Dictionary<string, WorkStepRequirementsAnalysisResult>? StepRequirements { get; set; }
}

public sealed class SaveProcessAnalysisResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("processDocumentId")]
    public Guid ProcessDocumentId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("totalSteps")]
    public int TotalSteps { get; set; }

    [JsonPropertyName("totalSkills")]
    public int TotalSkills { get; set; }

    [JsonPropertyName("storageContainer")]
    public string StorageContainer { get; set; } = string.Empty;

    [JsonPropertyName("storageBlobPath")]
    public string StorageBlobPath { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
