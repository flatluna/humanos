namespace HumanOS.Contracts.Capabilities;

/// <summary>
/// One standalone Capability (no Program) a person is enrolled in — built
/// from an explicit <c>CapabilityEnrollment</c> row (2026-07-24 design
/// decision: "Inscribirme" creates this row, "Empezar" never does — free
/// browsing/practicing stays possible without ever appearing here).
/// Progress fields are always computed live from the Capability Graph via
/// GraphProgressionEngine, never stored/duplicated. Powers "Mis
/// Capacidades", the sibling list to "Mis Programas".
/// </summary>
public sealed class EnrolledCapabilityResponse
{
    public Guid CapabilityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SubjectCode { get; set; }
    public string? SubjectName { get; set; }
    /// <summary>True when the capability has a cover image available.</summary>
    public bool HasCoverImage { get; set; }

    /// <summary>"job-skills" | "soft-skills" | "department-processes" |
    /// "hr-processes" | null. See Capability.Category. Powers the "tipo de
    /// curso" filter on the employee portal's /brain page (2026-08-02).</summary>
    public string? Category { get; set; }

    /// <summary>When the person enrolled — shown on the card as the
    /// "assigned"/enrollment date, same role as EnrolledProgramResponse's
    /// AcceptedDate.</summary>
    public DateTime EnrolledDate { get; set; }

    /// <summary>"NotStarted" | "InProgress" | "Completed" — derived from
    /// the Capability Graph's node mastery for this person.</summary>
    public string Status { get; set; } = "NotStarted";

    public int TotalNodes { get; set; }
    public int MasteredNodes { get; set; }

    /// <summary>Average FinalScore (0-100) across every node already
    /// Mastered in this capability — null while none are mastered yet.</summary>
    public int? AverageScore { get; set; }
}

/// <summary>Response for POST/DELETE .../enroll — just confirms the current
/// enrollment state, no progress fields (those come from GET
/// /people/{personId}/enrolled-capabilities).</summary>
public sealed class CapabilityEnrollmentActionResponse
{
    public Guid CapabilityId { get; set; }
    public bool Enrolled { get; set; }
    public DateTime? EnrolledDate { get; set; }
}
