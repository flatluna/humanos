using HumanOS.Models.People;

namespace HumanOS.Models.Culture;

public sealed class CulturePrincipleBehavior
{
    public Guid CulturePrincipleBehaviorId { get; set; }

    public Guid CulturePrincipleId { get; set; }

    /// <summary>
    /// Stable machine-readable code, e.g. APPLY_FEEDBACK. Unique within CulturePrincipleId.
    /// </summary>
    public string BehaviorCode { get; set; } = null!;

    /// <summary>
    /// Type of behavior expectation: ExpectedBehavior, BehaviorToAvoid, LeadershipExpectation,
    /// EmployeeExpectation, AITransformationImplication.
    /// </summary>
    public string BehaviorType { get; set; } = null!;

    public string DescriptionEn { get; set; } = null!;

    public string? DescriptionEs { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedByPersonId { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    /*
     * Navigation properties
     */

    public CulturePrinciple CulturePrinciple { get; set; } = null!;

    public Person? CreatedByPerson { get; set; }
}
