namespace HumanOS.Contracts.GrowthPlan;

public sealed class RecommendGrowthPathSubjectOption
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

/// <summary>Request body for POST /growth-plan/starting-point/recommend.
/// CatalogContext carries the frontend's current placeholder catalog
/// (mockLearningPrograms.ts + subjectGapSuggestions.ts, filtered to
/// AllowedSubjects) as a text blob — see GrowthPathRecommenderAgent for
/// how it's used.</summary>
public sealed class RecommendGrowthPathRequest
{
    public string GoalPrompt { get; set; } = string.Empty;

    public string PersonName { get; set; } = string.Empty;

    public List<RecommendGrowthPathSubjectOption> AllowedSubjects { get; set; } = [];

    public List<string> StatedGoals { get; set; } = [];

    public string? CatalogContext { get; set; }
}

public sealed class RecommendGrowthPathStepResponse
{
    public string Name { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;
}

public sealed class RecommendGrowthPathResponse
{
    public bool HasRecommendation { get; set; }

    public string RecommendationType { get; set; } = string.Empty;

    public string? ProgramName { get; set; }

    public string? ProgramDescription { get; set; }

    public string? SubjectCode { get; set; }

    public List<RecommendGrowthPathStepResponse> Steps { get; set; } = [];

    public string? Rationale { get; set; }

    /// <summary>Set when the agent matched a real, existing Program —
    /// the frontend can offer this as the ProgramId to persist on accept.</summary>
    public Guid? MatchedProgramId { get; set; }
}
