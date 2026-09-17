using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>
/// Root schema for Step 5: Determining learning content coverage and gaps.
/// </summary>
public sealed class LearningContentCoverageResponse
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
    /// "covered" | "partially_covered" | "content_gap"
    /// </summary>
    [JsonPropertyName("overallCoverageStatus")]
    public string OverallCoverageStatus { get; set; } = "content_gap";

    [JsonPropertyName("coverageSummary")]
    public string CoverageSummary { get; set; } = string.Empty;

    [JsonPropertyName("coveredRequirements")]
    public List<CoveredRequirementItem> CoveredRequirements { get; set; } = [];

    [JsonPropertyName("contentGaps")]
    public List<ContentGapItem> ContentGaps { get; set; } = [];

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("sourcePages")]
    public List<int> SourcePages { get; set; } = [];
}

public sealed class CoveredRequirementItem
{
    [JsonPropertyName("requirement")]
    public string Requirement { get; set; } = string.Empty;

    /// <summary>
    /// "critical" | "supporting"
    /// </summary>
    [JsonPropertyName("criticality")]
    public string Criticality { get; set; } = "critical";

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("skillId")]
    public string? SkillId { get; set; }

    [JsonPropertyName("isAiSkill")]
    public bool IsAiSkill { get; set; }

    [JsonPropertyName("matchedCapabilityName")]
    public string? MatchedCapabilityName { get; set; }

    [JsonPropertyName("supportingEngramIds")]
    public List<string> SupportingEngramIds { get; set; } = [];
}

public sealed class ContentGapItem
{
    [JsonPropertyName("requirement")]
    public string Requirement { get; set; } = string.Empty;

    /// <summary>
    /// "ability" | "soft_skill" | "business_skill" | "technical_skill" | "tool" | "completion_evidence" | "soft-skills" | "job-skills" | "department-processes" | "hr-processes"
    /// </summary>
    [JsonPropertyName("requirementType")]
    public string RequirementType { get; set; } = "ability";

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("skillId")]
    public string? SkillId { get; set; }

    [JsonPropertyName("isAiSkill")]
    public bool IsAiSkill { get; set; }

    [JsonPropertyName("isInCatalog")]
    public bool IsInCatalog { get; set; }

    [JsonPropertyName("hasCourse")]
    public bool HasCourse { get; set; }

    [JsonPropertyName("matchedCapabilityName")]
    public string? MatchedCapabilityName { get; set; }

    /// <summary>
    /// "critical" | "supporting"
    /// </summary>
    [JsonPropertyName("criticality")]
    public string Criticality { get; set; } = "critical";

    /// <summary>
    /// "partially_covered" | "not_covered"
    /// </summary>
    [JsonPropertyName("currentCoverage")]
    public string CurrentCoverage { get; set; } = "not_covered";

    [JsonPropertyName("gapReason")]
    public string GapReason { get; set; } = string.Empty;

    /// <summary>
    /// "none" | "new_engram_in_existing_class" | "new_class_required" | "new_skill_required" | "skill_in_catalog_course_pending"
    /// </summary>
    [JsonPropertyName("recommendedGapType")]
    public string RecommendedGapType { get; set; } = "new_class_required";

    [JsonPropertyName("existingLearningPathId")]
    public string? ExistingLearningPathId { get; set; }

    [JsonPropertyName("existingClassId")]
    public string? ExistingClassId { get; set; }

    [JsonPropertyName("proposedEngramTitle")]
    public string? ProposedEngramTitle { get; set; }

    [JsonPropertyName("targetClassName")]
    public string? TargetClassName { get; set; }

    /// <summary>
    /// "existing_class" | "proposed_class"
    /// </summary>
    [JsonPropertyName("targetClassType")]
    public string? TargetClassType { get; set; }
}

/// <summary>
/// Observability metrics for the Step 5 content coverage analysis.
/// </summary>
public sealed class LearningContentCoverageObservability
{
    public string DocumentId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string OverallStatus { get; set; } = string.Empty;
    public int CoveredCount { get; set; }
    public int GapCount { get; set; }
    public int NewEngramGapsCount { get; set; }
    public int NewClassGapsCount { get; set; }
    public TimeSpan Duration { get; set; }
    public string ModelDeployment { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int WarningsCount { get; set; }
}
