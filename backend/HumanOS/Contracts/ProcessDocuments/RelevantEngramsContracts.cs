using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>
/// Root schema for Step 4: Reading and selecting relevant Engrams from candidate Classes.
/// </summary>
public sealed class RelevantEngramsResponse
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("stepId")]
    public string StepId { get; set; } = string.Empty;

    [JsonPropertyName("stepNumber")]
    public string StepNumber { get; set; } = string.Empty;

    [JsonPropertyName("workStep")]
    public string WorkStep { get; set; } = string.Empty;

    [JsonPropertyName("selectedClasses")]
    public List<SelectedClassDto> SelectedClasses { get; set; } = [];

    [JsonPropertyName("requirementCoverage")]
    public List<RequirementCoverageItem> RequirementCoverage { get; set; } = [];

    [JsonPropertyName("uncoveredRequirements")]
    public List<string> UncoveredRequirements { get; set; } = [];

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("sourcePages")]
    public List<int> SourcePages { get; set; } = [];
}

public sealed class SelectedClassDto
{
    [JsonPropertyName("learningPathId")]
    public string LearningPathId { get; set; } = string.Empty;

    [JsonPropertyName("learningPathName")]
    public string LearningPathName { get; set; } = string.Empty;

    [JsonPropertyName("classId")]
    public string ClassId { get; set; } = string.Empty;

    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("classDescription")]
    public string ClassDescription { get; set; } = string.Empty;

    [JsonPropertyName("selectedEngrams")]
    public List<SelectedEngramDto> SelectedEngrams { get; set; } = [];
}

public sealed class SelectedEngramDto
{
    [JsonPropertyName("engramId")]
    public string EngramId { get; set; } = string.Empty;

    [JsonPropertyName("engramName")]
    public string EngramName { get; set; } = string.Empty;

    [JsonPropertyName("engramDescription")]
    public string EngramDescription { get; set; } = string.Empty;

    [JsonPropertyName("supportedRequirements")]
    public List<string> SupportedRequirements { get; set; } = [];

    [JsonPropertyName("relevanceReason")]
    public string RelevanceReason { get; set; } = string.Empty;

    [JsonPropertyName("relevanceScore")]
    public double RelevanceScore { get; set; }

    /// <summary>
    /// "critical" or "supporting".
    /// </summary>
    [JsonPropertyName("criticality")]
    public string Criticality { get; set; } = "supporting";
}

public sealed class RequirementCoverageItem
{
    [JsonPropertyName("requirement")]
    public string Requirement { get; set; } = string.Empty;

    /// <summary>
    /// "ability" | "soft_skill" | "business_skill" | "technical_skill" | "tool" | "completion_evidence"
    /// </summary>
    [JsonPropertyName("requirementType")]
    public string RequirementType { get; set; } = "ability";

    /// <summary>
    /// "covered" | "partially_covered" | "not_covered"
    /// </summary>
    [JsonPropertyName("coverageStatus")]
    public string CoverageStatus { get; set; } = "not_covered";

    [JsonPropertyName("supportingEngramIds")]
    public List<string> SupportingEngramIds { get; set; } = [];

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Lightweight metadata for an Engram in Engram Studio, retrieved without loading full lesson content.
/// </summary>
public sealed class EngramStudioEngramSummary
{
    public string EngramId { get; set; } = string.Empty;

    public string EngramName { get; set; } = string.Empty;

    public string EngramDescription { get; set; } = string.Empty;

    public string ClassId { get; set; } = string.Empty;

    public string LearningPathId { get; set; } = string.Empty;

    public string PublicationStatus { get; set; } = "published";

    public string Language { get; set; } = "es";

    public List<string> Tags { get; set; } = [];

    public Guid? OwnerTenantId { get; set; }

    public bool IsGlobal { get; set; }
}

/// <summary>
/// Observability execution metrics for the Engram selection step.
/// </summary>
public sealed class EngramSelectionObservability
{
    public string DocumentId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public int CandidateClassCount { get; set; }
    public int EngramCountRetrieved { get; set; }
    public int EngramCountSelected { get; set; }
    public int ClassesExcludedAfterEngramInspection { get; set; }
    public int CoveredRequirementCount { get; set; }
    public int PartiallyCoveredRequirementCount { get; set; }
    public int UncoveredRequirementCount { get; set; }
    public TimeSpan Duration { get; set; }
    public string ModelDeployment { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int WarningsCount { get; set; }
}
