namespace HumanOS.Contracts.GrowthPlan;

/// <summary>
/// One Capability ("course") inside a Program the person is enrolled in,
/// with its learning progress for THIS person scoped to THIS program
/// (LearningSession.ProgramId), per "una sesión por programa y por curso"
/// (2026-07-24) — the same Capability can appear in more than one Program
/// and each one tracks independent progress.
/// </summary>
public sealed class EnrolledProgramCapabilityResponse
{
    public Guid CapabilityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }

    /// <summary>"NotStarted" | "InProgress" | "Completed" — derived from
    /// the Capability Graph's node mastery for this person, scoped to this
    /// Program's LearningSession(s).</summary>
    public string Status { get; set; } = "NotStarted";

    public int TotalNodes { get; set; }
    public int MasteredNodes { get; set; }

    /// <summary>Average of this person's FinalScore across every node already
    /// Mastered in this capability (0-100) — null while none are mastered yet.</summary>
    public int? AverageScore { get; set; }
}

/// <summary>
/// One Program a person is enrolled in — built from the Growth Plan Step 3
/// ("Starting Point") accepted recommendation that resolved to a real
/// Program row (AcceptedRecommendation.ProgramId), which is the de facto
/// enrollment record (2026-07-24 design decision — no separate enrollment
/// table). Powers the "Mis Programas" card list.
/// </summary>
public sealed class EnrolledProgramResponse
{
    public Guid ProgramId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>When the person accepted this recommendation — shown on
    /// the card as the "assigned"/enrollment date.</summary>
    public DateTime AcceptedDate { get; set; }

    /// <summary>Average FinalScore (0-100) across every node this person has
    /// already Mastered so far, across ALL of this Program's capabilities —
    /// i.e. "how am I doing in this program up to today". Null while
    /// nothing has been mastered yet.</summary>
    public int? AverageScore { get; set; }

    public List<EnrolledProgramCapabilityResponse> Capabilities { get; set; } = [];
}
