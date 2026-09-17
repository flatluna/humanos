using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>
/// Root schema for Step 3: Searching Engram Studio for candidate classes.
/// </summary>
public sealed class CandidateClassSearchResponse
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("stepId")]
    public string StepId { get; set; } = string.Empty;

    [JsonPropertyName("stepNumber")]
    public string StepNumber { get; set; } = string.Empty;

    [JsonPropertyName("workStep")]
    public string WorkStep { get; set; } = string.Empty;

    [JsonPropertyName("candidateClasses")]
    public List<CandidateClassDto> CandidateClasses { get; set; } = [];

    [JsonPropertyName("requirementsWithoutCandidateClass")]
    public List<string> RequirementsWithoutCandidateClass { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("sourcePages")]
    public List<int> SourcePages { get; set; } = [];
}

public sealed class CandidateClassDto
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

    [JsonPropertyName("publicationStatus")]
    public string PublicationStatus { get; set; } = "published";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "es";

    [JsonPropertyName("matchedAbilities")]
    public List<string> MatchedAbilities { get; set; } = [];

    [JsonPropertyName("matchedSkillRequirements")]
    public List<MatchedSkillRequirement> MatchedSkillRequirements { get; set; } = [];

    [JsonPropertyName("relevanceReason")]
    public string RelevanceReason { get; set; } = string.Empty;

    [JsonPropertyName("relevanceScore")]
    public double RelevanceScore { get; set; }

    [JsonPropertyName("limitations")]
    public List<string> Limitations { get; set; } = [];
}

public sealed class MatchedSkillRequirement
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty; // "soft" | "business" | "technical" | "tool"

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Lightweight metadata for a Class in Engram Studio, retrieved without loading Engrams.
/// </summary>
public sealed class EngramStudioClassSummary
{
    public string LearningPathId { get; set; } = string.Empty;

    public string LearningPathName { get; set; } = string.Empty;

    public string ClassId { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string ClassDescription { get; set; } = string.Empty;

    public List<string> CapabilityOrTopicTags { get; set; } = [];

    public string PublicationStatus { get; set; } = "published"; // published, draft, retired, deleted

    public string Language { get; set; } = "es";

    public int EngramCount { get; set; }

    public Guid? OwnerTenantId { get; set; }

    public bool IsGlobal { get; set; }
}

/// <summary>
/// Observability execution metrics for the candidate Class search.
/// </summary>
public sealed class CandidateClassSearchObservability
{
    public string DocumentId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public int ClassesConsidered { get; set; }
    public int ClassesReturned { get; set; }
    public int UnmatchedRequirementsCount { get; set; }
    public TimeSpan Duration { get; set; }
    public string ModelDeployment { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int WarningsCount { get; set; }
}
