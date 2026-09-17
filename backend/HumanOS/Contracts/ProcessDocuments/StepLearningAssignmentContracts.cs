using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>
/// Root schema for Step 7: Standard Step Learning Assignment.
/// </summary>
public sealed class StepLearningAssignmentResponse
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("stepId")]
    public string StepId { get; set; } = string.Empty;

    [JsonPropertyName("stepNumber")]
    public string StepNumber { get; set; } = string.Empty;

    [JsonPropertyName("workStep")]
    public string WorkStep { get; set; } = string.Empty;

    /// <summary>
    /// "ready" | "ready_with_content_gaps" | "blocked_by_content_gap"
    /// </summary>
    [JsonPropertyName("assignmentStatus")]
    public string AssignmentStatus { get; set; } = "ready";

    [JsonPropertyName("assignmentMarkdown")]
    public string AssignmentMarkdown { get; set; } = string.Empty;

    [JsonPropertyName("learningPathIds")]
    public List<string> LearningPathIds { get; set; } = [];

    [JsonPropertyName("classIds")]
    public List<string> ClassIds { get; set; } = [];

    [JsonPropertyName("engramIds")]
    public List<string> EngramIds { get; set; } = [];

    /// <summary>
    /// "covered" | "partially_covered" | "content_gap"
    /// </summary>
    [JsonPropertyName("coverageStatus")]
    public string CoverageStatus { get; set; } = "covered";

    [JsonPropertyName("contentGapDraftReferences")]
    public List<string> ContentGapDraftReferences { get; set; } = [];

    [JsonPropertyName("blockingContentGap")]
    public bool BlockingContentGap { get; set; }

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("sourcePages")]
    public List<int> SourcePages { get; set; } = [];
}

/// <summary>
/// Observability execution metrics for the Step Learning Assignment generator.
/// </summary>
public sealed class StepLearningAssignmentObservability
{
    public string DocumentId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string AssignmentStatus { get; set; } = string.Empty;
    public string CoverageStatus { get; set; } = string.Empty;
    public int LearningPathCount { get; set; }
    public int ClassCount { get; set; }
    public int EngramCount { get; set; }
    public int ContentGapCount { get; set; }
    public bool BlockingContentGap { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public int WarningsCount { get; set; }
}
