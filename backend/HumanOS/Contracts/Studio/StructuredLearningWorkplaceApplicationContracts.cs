namespace HumanOS.Contracts.Studio;

public sealed class StructuredLearningWorkplaceApplicationContext
{
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public Guid SectionId { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public string SubchapterTitle { get; set; } = string.Empty;
    public string Capability { get; set; } = string.Empty;
    public string TeachingContent { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = "es";

    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string RoleTitle { get; set; } = string.Empty;
    public string RoleMission { get; set; } = string.Empty;
    public int? YearsOfExperience { get; set; }
    public List<string> ToolsAndTechnologies { get; set; } = [];
    public List<string> ActiveObjectives { get; set; } = [];
    public List<string> Kpis { get; set; } = [];
    public List<string> TechnicalSkills { get; set; } = [];
    public string? EngramImpactTier { get; set; }
    public string? EngramPersonalizationRationale { get; set; }
    public bool IsStudent { get; set; }
    public string? StudentAcademicProfileSummary { get; set; }

    public string UserInstructions { get; set; } = string.Empty;
}

public sealed class StructuredLearningWorkplaceApplicationResult
{
    public string ScenarioTitle { get; set; } = string.Empty;
    public string ScenarioDescription { get; set; } = string.Empty;
    public string SpokenAudioScript { get; set; } = string.Empty;
    public List<string> PracticalActions { get; set; } = [];
    public string RelevanceSummary { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "es";

    public string WhyThisMatters { get; set; } = string.Empty;
    public List<StructuredLearningWorkedExample> WorkedExamples { get; set; } = [];
    public List<string> KnowledgeConnections { get; set; } = [];
}

public sealed class StructuredLearningWorkedExample
{
    public string Title { get; set; } = string.Empty;
    public string Situation { get; set; } = string.Empty;
    public string InputsAndAssumptions { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = [];
    public string Artifact { get; set; } = string.Empty;
    public string ArtifactLanguage { get; set; } = string.Empty;
    public string ExpectedResult { get; set; } = string.Empty;
    public string Validation { get; set; } = string.Empty;
}

public sealed class StructuredLearningWorkplaceApplicationResponse
{
    public Guid SectionId { get; set; }
    public Guid PersonId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string RoleTitle { get; set; } = string.Empty;
    public List<string> Tools { get; set; } = [];
    public string? TargetObjective { get; set; }
    public string ScenarioTitle { get; set; } = string.Empty;
    public string ScenarioText { get; set; } = string.Empty;
    public List<string> PracticalActions { get; set; } = [];
    public string AudioUrl { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "es";

    public string UserInstructions { get; set; } = string.Empty;
    public string WhyThisMatters { get; set; } = string.Empty;
    public List<StructuredLearningWorkedExample> WorkedExamples { get; set; } = [];
    public List<string> KnowledgeConnections { get; set; } = [];

    /// <summary>Bumped whenever the agent's output format/rules change materially — cached
    /// responses with an older (or missing/0) version are treated as stale and regenerated.</summary>
    public int SchemaVersion { get; set; }
}

/// <summary>Generated scenario + real token usage from one agent call — see
/// <see cref="HumanOS.Agents.Studio.StructuredLearningWorkplaceApplicationAgent.GenerateScenarioAsync"/>
/// Only produced when the agent actually runs (never on a cache hit),
/// so cost is only ever counted once per real generation.</summary>
public sealed class StructuredLearningWorkplaceApplicationAgentResult
{
    public StructuredLearningWorkplaceApplicationResult Result { get; set; } = null!;

    public string ModelName { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }
}
