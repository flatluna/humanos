using HumanOS.Agents;

namespace HumanOS.Contracts.GrowthPlan;

/// <summary>Response for Current Situation step (Step 1).</summary>
public sealed class GetCurrentSituationResponse
{
    public string[] SelectedSubjectCodes { get; set; } = [];

    public Dictionary<string, string> SelfAssessedLevelBySubject { get; set; } = [];

    public bool Completed { get; set; }
}

/// <summary>Request to save/update Current Situation.</summary>
public sealed class UpsertCurrentSituationRequest
{
    public string[] SelectedSubjectCodes { get; set; } = [];

    public Dictionary<string, string> SelfAssessedLevelBySubject { get; set; } = [];

    public bool Completed { get; set; }
}

/// <summary>Response for Future Direction step (Step 2).</summary>
public sealed class GetFutureDirectionResponse
{
    public string[] SelectedGoalIds { get; set; } = [];

    public string[] SelectedMotivationCodes { get; set; } = [];

    public bool Completed { get; set; }
}

/// <summary>Request to save/update Future Direction.</summary>
public sealed class UpsertFutureDirectionRequest
{
    public string[] SelectedGoalIds { get; set; } = [];

    public string[] SelectedMotivationCodes { get; set; } = [];

    public bool Completed { get; set; }
}

/// <summary>One agent-recommended Program/Capabilities snapshot the person
/// accepted for a given subject, in Growth Plan Step 3. This is a frozen
/// copy of what the agent proposed — not a live link. <see cref="ProgramId"/>
/// stays null until a future "activation" step creates a real Program row.</summary>
public sealed class AcceptedRecommendation
{
    public string SubjectCode { get; set; } = string.Empty;

    /// <summary>"Program" or "Capabilities" — same meaning as
    /// GrowthPathRecommendation.RecommendationType.</summary>
    public string RecommendationType { get; set; } = string.Empty;

    public string ProgramName { get; set; } = string.Empty;

    public string ProgramDescription { get; set; } = string.Empty;

    public List<RecommendedProgramStep> Steps { get; set; } = [];

    public string Rationale { get; set; } = string.Empty;

    /// <summary>Null until this recommendation is later activated into a
    /// real Program row.</summary>
    public Guid? ProgramId { get; set; }
}

/// <summary>Response for Starting Point step (Step 3).</summary>
public sealed class GetStartingPointResponse
{
    public string[] SelectedCapabilityIds { get; set; } = [];

    public Dictionary<string, List<string>> GapCapabilitiesBySubject { get; set; } = [];

    public List<AcceptedRecommendation> AcceptedRecommendations { get; set; } = [];

    public bool Completed { get; set; }
}

/// <summary>Request to save/update Starting Point.</summary>
public sealed class UpsertStartingPointRequest
{
    public string[] SelectedCapabilityIds { get; set; } = [];

    public Dictionary<string, List<string>> GapCapabilitiesBySubject { get; set; } = [];

    public List<AcceptedRecommendation> AcceptedRecommendations { get; set; } = [];

    public bool Completed { get; set; }
}
