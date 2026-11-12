using HumanOS.Models.People;
using HumanOS.Models.Tenancy;

namespace HumanOS.Models.Culture;

public sealed class CulturePrinciple
{
    public Guid CulturePrincipleId { get; set; }

    /// <summary>
    /// Null for global EngramsIQ principles. Populated for customer-owned principles.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Self-referencing FK used when a customer copies and customizes a global principle.
    /// </summary>
    public Guid? SourceCulturePrincipleId { get; set; }

    /// <summary>
    /// Stable machine-readable code, e.g. GROWTH_MINDSET.
    /// </summary>
    public string PrincipleCode { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string? NameEs { get; set; }

    public string DescriptionEn { get; set; } = null!;

    public string? DescriptionEs { get; set; }

    /// <summary>
    /// Business owner of the principle.
    /// </summary>
    public Guid? OwnerPersonId { get; set; }

    public int VersionNumber { get; set; } = 1;

    /// <summary>
    /// Lifecycle state: Draft, Approved, Superseded, Retired.
    /// </summary>
    public string Status { get; set; } = CulturePrincipleStatus.Draft;

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public Guid? ApprovedByPersonId { get; set; }

    public DateTime? ApprovedAt { get; set; }

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

    public Tenant? Tenant { get; set; }

    public CulturePrinciple? SourceCulturePrinciple { get; set; }

    public ICollection<CulturePrinciple> AdaptedPrinciples { get; set; } = [];

    public Person? OwnerPerson { get; set; }

    public Person? ApprovedByPerson { get; set; }

    public Person? CreatedByPerson { get; set; }

    public ICollection<CulturePrincipleBehavior> Behaviors { get; set; } = [];
}
