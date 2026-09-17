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
