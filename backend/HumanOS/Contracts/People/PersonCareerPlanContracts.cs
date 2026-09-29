namespace HumanOS.Contracts.People;

/// <summary>One ordered step in a track-specific recommended training
/// roadmap — mirrors <see cref="HumanOS.Agents.CareerPlanRoadmapStep"/>
/// after defensive re-validation of MatchedProgramId. When
/// MatchedProgramId is null, no real course exists yet for this step and
/// HR is expected to create/upload one later (Title/Description describe
/// what it should cover).</summary>
public sealed class CareerPlanRoadmapStepDto
{
    public int Order { get; set; }

    public string Skill { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid? MatchedProgramId { get; set; }

    /// <summary>Real Program name, looked up from the catalog — never
    /// trusts the agent's own name, only present when MatchedProgramId
    /// is set.</summary>
    public string? MatchedProgramName { get; set; }

    /// <summary>Set automatically (never by a human) when a standalone
    /// Capability (no wrapping Program) whose Name exactly matches this
    /// step's Title already exists in the catalog — see
    /// PersonCareerPlanService.AutoMatchCapabilitiesAsync, which runs on
    /// every plan read and auto-enrolls the employee once found, so HR
    /// never has to manually link anything.</summary>
    public Guid? MatchedCapabilityId { get; set; }

    public string? MatchedCapabilityName { get; set; }

    /// <summary>"es"/"en" of the matched course — resolved live on every
    /// plan read (see PersonCareerPlanService.ApplyTrainingStatusAsync)
    /// from Capability.ContentLanguageCode or the Structured Learning
    /// document's own ConfigSnapshotJson/StructuredLearningLanguageHeuristic.
    /// Null when no course is matched yet.</summary>
    public string? Language { get; set; }

    /// <summary>"Completed" | "InProgress" | "Pending" — this employee's
    /// real progress on MatchedProgramId/MatchedCapabilityId, derived live
    /// from their enrollment/Capability Graph mastery (see
    /// PersonCareerPlanService.ApplyTrainingStatusAsync). Always "Pending"
    /// when neither is set (no real course exists yet).</summary>
    public string TrainingStatus { get; set; } = "Pending";
}

/// <summary>GET /people/{personId}/career-plan response — the latest
/// saved plan, or an empty/not-generated response if none exists yet.</summary>
public sealed class PersonCareerPlanResponse
{
    public Guid PersonId { get; set; }

    public bool HasPlan { get; set; }

    public Guid? JobRoleId { get; set; }

    public string? JobRoleTitle { get; set; }

    public List<string> GapSkills { get; set; } = [];

    /// <summary>Ordered training roadmap for the required TECHNICAL skill
    /// gaps only.</summary>
    public List<CareerPlanRoadmapStepDto> TechnicalRoadmap { get; set; } = [];

    /// <summary>Ordered training roadmap for the required SOFT skill gaps
    /// only.</summary>
    public List<CareerPlanRoadmapStepDto> SoftSkillsRoadmap { get; set; } = [];

    /// <summary>Ordered training roadmap for the required DEPARTMENT
    /// PROCESS gaps only ("Procesos de Depto." career-plan track).</summary>
    public List<CareerPlanRoadmapStepDto> DepartmentProcessesRoadmap { get; set; } = [];

    /// <summary>Ordered training roadmap for the required HR PROCESS gaps
    /// only ("Procesos de RRHH" career-plan track).</summary>
    public List<CareerPlanRoadmapStepDto> HrProcessesRoadmap { get; set; } = [];

    public string? OverallRationale { get; set; }

    public DateTime? GeneratedDate { get; set; }

    /// <summary>True once HR has confirmed this plan via "Aceptar plan".</summary>
    public bool IsAccepted { get; set; }

    public DateTime? AcceptedDate { get; set; }
}
