namespace HumanOS.Contracts.Programs;

/// <summary>Response for POST/DELETE .../enroll — just confirms the current
/// enrollment state, no progress fields (those come from GET
/// /people/{personId}/enrolled-programs). Mirrors
/// CapabilityEnrollmentActionResponse's role for standalone Capabilities.</summary>
public sealed class ProgramEnrollmentActionResponse
{
    public Guid ProgramId { get; set; }
    public bool Enrolled { get; set; }
    public DateTime? EnrolledDate { get; set; }
}
