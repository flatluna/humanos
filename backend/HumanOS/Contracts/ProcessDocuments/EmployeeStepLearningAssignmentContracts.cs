using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>
/// Root schema for Step 8: Personalized Employee Step Learning Assignment.
/// </summary>
public sealed class EmployeeStepLearningAssignmentResponse
{
    [JsonPropertyName("assignmentId")]
    public string AssignmentId { get; set; } = string.Empty;

    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; } = string.Empty;

    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("stepId")]
    public string StepId { get; set; } = string.Empty;

    [JsonPropertyName("stepNumber")]
    public string StepNumber { get; set; } = string.Empty;

    [JsonPropertyName("workStep")]
    public string WorkStep { get; set; } = string.Empty;

    /// <summary>
    /// "assigned" | "assigned_with_content_gaps" | "blocked_by_content_gap"
    /// </summary>
    [JsonPropertyName("employeeAssignmentStatus")]
    public string EmployeeAssignmentStatus { get; set; } = "assigned";

    [JsonPropertyName("employeeMustBeAbleTo")]
    public List<string> EmployeeMustBeAbleTo { get; set; } = [];

    [JsonPropertyName("reportedExperienceSummary")]
    public List<string> ReportedExperienceSummary { get; set; } = [];

    [JsonPropertyName("assignedClasses")]
    public List<AssignedClassDto> AssignedClasses { get; set; } = [];

    [JsonPropertyName("expectedWorkEvidence")]
    public List<string> ExpectedWorkEvidence { get; set; } = [];

    [JsonPropertyName("contentGaps")]
    public List<AssignmentContentGapDto> ContentGaps { get; set; } = [];

    [JsonPropertyName("assignmentMarkdown")]
    public string AssignmentMarkdown { get; set; } = string.Empty;

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AssignedClassDto
{
    [JsonPropertyName("learningPathId")]
    public string LearningPathId { get; set; } = string.Empty;

    [JsonPropertyName("learningPathName")]
    public string LearningPathName { get; set; } = string.Empty;

    [JsonPropertyName("classId")]
    public string ClassId { get; set; } = string.Empty;

    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// "required" | "recommended" | "reference_only"
    /// </summary>
    [JsonPropertyName("classAssignmentStatus")]
    public string ClassAssignmentStatus { get; set; } = "required";

    [JsonPropertyName("assignedEngrams")]
    public List<AssignedEngramDto> AssignedEngrams { get; set; } = [];
}

public sealed class AssignedEngramDto
{
    [JsonPropertyName("engramId")]
    public string EngramId { get; set; } = string.Empty;

    [JsonPropertyName("engramName")]
    public string EngramName { get; set; } = string.Empty;

    /// <summary>
    /// "required" | "recommended" | "reference_only" | "previously_completed" | "unavailable_content_gap"
    /// </summary>
    [JsonPropertyName("assignmentStatus")]
    public string AssignmentStatus { get; set; } = "required";

    [JsonPropertyName("assignmentReason")]
    public string AssignmentReason { get; set; } = string.Empty;

    /// <summary>
    /// "unknown" | "reported" | "completed" | "demonstrated"
    /// </summary>
    [JsonPropertyName("employeeEvidenceLevel")]
    public string EmployeeEvidenceLevel { get; set; } = "unknown";

    [JsonPropertyName("employeeEvidenceSummary")]
    public string EmployeeEvidenceSummary { get; set; } = string.Empty;
}

public sealed class AssignmentContentGapDto
{
    [JsonPropertyName("missingCapability")]
    public string MissingCapability { get; set; } = string.Empty;

    /// <summary>
    /// "new_engram_in_existing_class" | "new_class_required"
    /// </summary>
    [JsonPropertyName("contentRequired")]
    public string ContentRequired { get; set; } = "new_engram_in_existing_class";

    [JsonPropertyName("draftReference")]
    public string DraftReference { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "needs_expert_review";

    [JsonPropertyName("blocking")]
    public bool Blocking { get; set; }
}

public sealed class CreateEmployeeAssignmentRequest
{
    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; } = string.Empty;
}
