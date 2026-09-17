using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>
/// Root schema for Step 6: Reviewable content gap drafts.
/// </summary>
public sealed class LearningContentGapDraftResponse
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("stepId")]
    public string StepId { get; set; } = string.Empty;

    [JsonPropertyName("stepNumber")]
    public string StepNumber { get; set; } = string.Empty;

    [JsonPropertyName("workStep")]
    public string WorkStep { get; set; } = string.Empty;

    [JsonPropertyName("overallCoverageStatus")]
    public string OverallCoverageStatus { get; set; } = "covered";

    [JsonPropertyName("draftCreated")]
    public bool DraftCreated { get; set; }

    [JsonPropertyName("engramDrafts")]
    public List<EngramDraftDto> EngramDrafts { get; set; } = [];

    [JsonPropertyName("classDrafts")]
    public List<ClassDraftDto> ClassDrafts { get; set; } = [];

    [JsonPropertyName("catalogItemsChecked")]
    public List<CatalogItemCheckedDto> CatalogItemsChecked { get; set; } = [];

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("sourcePages")]
    public List<int> SourcePages { get; set; } = [];
}

public sealed class EngramDraftDto
{
    [JsonPropertyName("draftReference")]
    public string DraftReference { get; set; } = string.Empty;

    [JsonPropertyName("proposedEngramTitle")]
    public string ProposedEngramTitle { get; set; } = string.Empty;

    [JsonPropertyName("proposedEngramDescription")]
    public string ProposedEngramDescription { get; set; } = string.Empty;

    [JsonPropertyName("capabilitySupported")]
    public string CapabilitySupported { get; set; } = string.Empty;

    [JsonPropertyName("workStepRequirementSupported")]
    public string WorkStepRequirementSupported { get; set; } = string.Empty;

    [JsonPropertyName("reasonNeeded")]
    public string ReasonNeeded { get; set; } = string.Empty;

    [JsonPropertyName("parentLearningPathId")]
    public string ParentLearningPathId { get; set; } = string.Empty;

    [JsonPropertyName("parentLearningPathName")]
    public string ParentLearningPathName { get; set; } = string.Empty;

    [JsonPropertyName("parentClassId")]
    public string ParentClassId { get; set; } = string.Empty;

    [JsonPropertyName("parentClassName")]
    public string ParentClassName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "needs_expert_review";
}

public sealed class ClassDraftDto
{
    [JsonPropertyName("draftReference")]
    public string DraftReference { get; set; } = string.Empty;

    [JsonPropertyName("proposedClassTitle")]
    public string ProposedClassTitle { get; set; } = string.Empty;

    [JsonPropertyName("proposedClassDescription")]
    public string ProposedClassDescription { get; set; } = string.Empty;

    [JsonPropertyName("capabilityDeveloped")]
    public string CapabilityDeveloped { get; set; } = string.Empty;

    [JsonPropertyName("workStepRequirementsSupported")]
    public List<string> WorkStepRequirementsSupported { get; set; } = [];

    [JsonPropertyName("recommendedExistingLearningPathId")]
    public string? RecommendedExistingLearningPathId { get; set; }

    [JsonPropertyName("recommendedExistingLearningPathName")]
    public string? RecommendedExistingLearningPathName { get; set; }

    [JsonPropertyName("proposedLearningPathName")]
    public string? ProposedLearningPathName { get; set; }

    [JsonPropertyName("reasonNewClassIsRequired")]
    public string ReasonNewClassIsRequired { get; set; } = string.Empty;

    [JsonPropertyName("proposedEngramIndex")]
    public List<ProposedEngramIndexItemDto> ProposedEngramIndex { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = "needs_expert_review";
}

public sealed class ProposedEngramIndexItemDto
{
    [JsonPropertyName("proposedEngramTitle")]
    public string ProposedEngramTitle { get; set; } = string.Empty;

    [JsonPropertyName("proposedEngramDescription")]
    public string ProposedEngramDescription { get; set; } = string.Empty;

    [JsonPropertyName("capabilitySupported")]
    public string CapabilitySupported { get; set; } = string.Empty;

    [JsonPropertyName("workStepRequirementSupported")]
    public string WorkStepRequirementSupported { get; set; } = string.Empty;

    [JsonPropertyName("suggestedOrder")]
    public int SuggestedOrder { get; set; }
}

public sealed class CatalogItemCheckedDto
{
    [JsonPropertyName("itemType")]
    public string ItemType { get; set; } = "class"; // "learning_path" | "class" | "engram"

    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;
}

public sealed class EngramStudioLearningPathSummary
{
    public string LearningPathId { get; set; } = string.Empty;
    public string LearningPathName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Observability execution metrics for content gap draft generation.
/// </summary>
public sealed class ContentGapDraftObservability
{
    public string DocumentId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public bool DraftCreated { get; set; }
    public int EngramDraftsCount { get; set; }
    public int ClassDraftsCount { get; set; }
    public int CatalogItemsCheckedCount { get; set; }
    public TimeSpan Duration { get; set; }
    public string ModelDeployment { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int WarningsCount { get; set; }
}
