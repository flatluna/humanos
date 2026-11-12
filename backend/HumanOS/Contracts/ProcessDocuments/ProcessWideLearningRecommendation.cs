using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>
/// Output schema for a SINGLE, whole-process learning recommendation: the LLM is given
/// every chapter/step of the process at once and returns one consolidated, de-duplicated
/// set of skill/knowledge gaps (instead of one analysis call per step).
/// </summary>
public sealed class ProcessWideLearningRecommendationResult
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("processName")]
    public string ProcessName { get; set; } = string.Empty;

    [JsonPropertyName("overallSummary")]
    public string OverallSummary { get; set; } = string.Empty;

    [JsonPropertyName("skillGaps")]
    public List<ProcessSkillGapItem> SkillGaps { get; set; } = [];

    [JsonPropertyName("assumptions")]
    public List<string> Assumptions { get; set; } = [];

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    /// <summary>UTC timestamp of the last time this recommendation was produced by the LLM. Preserved
    /// (not overwritten) when only manual skill additions/removals are saved; only updated when the
    /// whole-process analysis is actually re-run.</summary>
    [JsonPropertyName("analyzedAt")]
    public DateTime? AnalyzedAt { get; set; }
}

public sealed class ProcessSkillGapItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// "soft-skills" | "technology-skills"
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = "soft-skills";

    [JsonPropertyName("isAiSkill")]
    public bool IsAiSkill { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Step numbers/names across the whole process where this gap applies (for traceability only).
    /// </summary>
    [JsonPropertyName("affectedSteps")]
    public List<string> AffectedSteps { get; set; } = [];

    [JsonPropertyName("recommendedCourses")]
    public List<RecommendedCourseItem> RecommendedCourses { get; set; } = [];

    /// <summary>True when this gap is already covered by an existing dbo.Skill catalog entry
    /// (matched by the catalog-matching pass) — no internet search was run for it and
    /// "recommendedCourses" stays empty; the UI should offer "assign" only, not "create".</summary>
    [JsonPropertyName("isFromCatalog")]
    public bool IsFromCatalog { get; set; }

    [JsonPropertyName("matchedSkillId")]
    public string? MatchedSkillId { get; set; }

    [JsonPropertyName("matchedSkillName")]
    public string? MatchedSkillName { get; set; }

    /// <summary>True when the user manually assigned this skill from the catalog (instead of the LLM
    /// identifying it). Preserved across re-analysis so manual choices are not discarded.</summary>
    [JsonPropertyName("isManual")]
    public bool IsManual { get; set; }
}

public sealed class RecommendedCourseItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

/// <summary>
/// Output schema for the course-assignment pass: one entry per already-identified skill gap,
/// matched strictly by name, with ONLY the real courses found in that gap's own labeled
/// internet-search section (never invented, never borrowed from another skill's section).
/// </summary>
public sealed class CourseAssignmentResult
{
    [JsonPropertyName("assignments")]
    public List<SkillGapCourseAssignment> Assignments { get; set; } = [];
}

public sealed class SkillGapCourseAssignment
{
    /// <summary>Must exactly match the "name" of one of the input skill gaps.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("recommendedCourses")]
    public List<RecommendedCourseItem> RecommendedCourses { get; set; } = [];

    /// <summary>Set when no real course was found for this specific skill (e.g. "No verified internet course found for X").</summary>
    [JsonPropertyName("warning")]
    public string? Warning { get; set; }
}

/// <summary>
/// Output schema for the PHASE 0 catalog-matching pass: given the fixed list of skill gaps and the
/// tenant's existing dbo.Skill catalog (soft-skills + job-skills, name + description only), the LLM
/// decides which gaps are ALREADY covered by an existing catalog skill so no internet search or new
/// course needs to be created for them.
/// </summary>
public sealed class CatalogMatchResult
{
    [JsonPropertyName("matches")]
    public List<SkillGapCatalogMatch> Matches { get; set; } = [];
}

public sealed class SkillGapCatalogMatch
{
    /// <summary>Must exactly match the "name" of one of the input skill gaps.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isFromCatalog")]
    public bool IsFromCatalog { get; set; }

    /// <summary>The exact "skillId" given in the input catalog list for the matched entry.
    /// Required when isFromCatalog is true.</summary>
    [JsonPropertyName("matchedSkillId")]
    public string? MatchedSkillId { get; set; }

    [JsonPropertyName("matchedSkillName")]
    public string? MatchedSkillName { get; set; }
}

/// <summary>A single dbo.Skill catalog entry passed to the catalog-matching LLM pass.</summary>
public sealed class CatalogSkillEntry
{
    public string SkillId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Observability execution metrics for the whole-process learning recommendation.
/// </summary>
public sealed class ProcessWideLearningRecommendationObservability
{
    public string DocumentId { get; set; } = string.Empty;
    public string ModelDeployment { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public bool WebGroundingUsed { get; set; }
    public int SkillGapsCount { get; set; }
    public int WarningsCount { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
