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

    /// <summary>CQI Workspace profile for the employee (non-student businesses only) — null
    /// when not uploaded/available. Consumed only by <see cref="HumanOS.Agents.Studio.StructuredLearningWorkplaceApplicationCqiAgent"/>.</summary>
    public HumanOS.Contracts.People.CqiProfileData? CqiProfile { get; set; }
}

public sealed class StructuredLearningWorkplaceApplicationResult
{
    public string ScenarioTitle { get; set; } = string.Empty;
    public string ScenarioDescription { get; set; } = string.Empty;
    public string SpokenAudioScript { get; set; } = string.Empty;
    public List<string> PracticalActions { get; set; } = [];
    public string RelevanceSummary { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "es";

    // --- CQI-aware employee sections (empty for the plain/student agent) ---
    public string WhyThisMatters { get; set; } = string.Empty;
    public List<string> ReflectionQuestions { get; set; } = [];
    public string GrowthOpportunity { get; set; } = string.Empty;
    public List<string> KnowledgeConnections { get; set; } = [];

    /// <summary>Exactly 2 short grounded paragraphs explaining a concrete worked example of applying
    /// this engram in the employee's role — paired 1:1 with the 2 generated CQI Type 6 example images
    /// (index 0/1). Empty for the plain/student agent.</summary>
    public List<string> ExampleImageExplanations { get; set; } = [];
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

    // --- CQI-aware employee sections (empty for the plain/student agent) ---
    public string WhyThisMatters { get; set; } = string.Empty;
    public List<string> ReflectionQuestions { get; set; } = [];
    public string GrowthOpportunity { get; set; } = string.Empty;
    public List<string> KnowledgeConnections { get; set; } = [];

    /// <summary>Bumped whenever the CQI agent's output format/rules change materially — cached
    /// responses with an older (or missing/0) version are treated as stale and regenerated.</summary>
    public int SchemaVersion { get; set; }

    // --- CQI Type 6 ("The Verifier") recognition — employee/business tenants only, shown inline ---

    /// <summary>E.g. "Type 6" — set only when the employee's CQI leading pattern is Type 6.</summary>
    public string? CqiLeadingPatternType { get; set; }

    /// <summary>E.g. "The Verifier".</summary>
    public string? CqiLeadingPatternName { get; set; }

    /// <summary>Code-generated label sentence naming the type/pattern (e.g. "Type 6: The Verifier") —
    /// the agent's own text never names the type, but this line is shown plainly inline on the page.</summary>
    public string? CqiTypeLabel { get; set; }

    /// <summary>The employee's own "Learning and Working Preferences" CQI text, shown inline on the page.</summary>
    public string? CqiLearningAndWorkingPreferences { get; set; }

    /// <summary>Two additional generated example images (job-role-specific applications of this
    /// engram), served via GetStructuredLearningWorkplaceApplicationCqiExampleImage.</summary>
    public List<string> CqiExampleImageUrls { get; set; } = [];

    /// <summary>Explanation text paired 1:1 (by index) with <see cref="CqiExampleImageUrls"/> —
    /// what each image depicts and how it expands on the lesson, so the images are never shown bare.</summary>
    public List<string> CqiExampleImageExplanations { get; set; } = [];
}
