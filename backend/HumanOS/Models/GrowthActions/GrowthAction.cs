using HumanOS.Models.Assessments;
using HumanOS.Models.Capabilities;
using HumanOS.Models.People;
using HumanOS.Models.Practice;
using HumanOS.Models.Recall;
using HumanOS.Models.Tenancy;

namespace HumanOS.Models.GrowthActions;

/// <summary>
/// A single "action for today" (recall / practice / challenge) that the UI
/// reads to drive the daily action list. Optionally links to the existing
/// detail record (RecallAttempt / CapabilityPractice / Assessment) once the
/// user completes it.
/// </summary>
public sealed class GrowthAction
{
    public Guid GrowthActionId { get; set; }

    /// <summary>The universal "user id" — owner of this action.</summary>
    public Guid PersonId { get; set; }

    public Guid TenantId { get; set; }

    public Guid PersonCapabilityId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>'recall' | 'practice' | 'challenge'.</summary>
    public string ActionType { get; set; } = null!;

    public bool IsCompleted { get; set; }

    public DateOnly? ScheduledFor { get; set; }

    /*
     * Optional links to existing detail tables
     */

    public Guid? RecallAttemptId { get; set; }

    public Guid? PracticeId { get; set; }

    public Guid? AssessmentId { get; set; }

    public DateTime CreatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Person Person { get; set; } = null!;

    public Tenant Tenant { get; set; } = null!;

    public PersonCapability PersonCapability { get; set; } = null!;

    public RecallAttempt? RecallAttempt { get; set; }

    public CapabilityPractice? Practice { get; set; }

    public Assessment? Assessment { get; set; }
}
