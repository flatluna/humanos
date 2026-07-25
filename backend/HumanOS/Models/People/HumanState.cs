using HumanOS.Models.Tenancy;

namespace HumanOS.Models.People;

/// <summary>
/// Daily snapshot of a person's human state (energy, focus, streak) —
/// distinct from <see cref="HumanProfile"/>, which holds static/slow-changing
/// profile data. One row per day per person, used for "Today" dashboards
/// (Apple Health-style).
/// </summary>
public sealed class HumanState
{
    public Guid HumanStateId { get; set; }

    /// <summary>The universal "user id" used as the FK across every table in this schema.</summary>
    public Guid PersonId { get; set; }

    /// <summary>Null for individuals not part of an organization (mirrors <see cref="Person.TenantId"/>'s nullability).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>0-100.</summary>
    public int? Energy { get; set; }

    /// <summary>0-100.</summary>
    public int? Focus { get; set; }

    /// <summary>0-100. Sense of meaning/direction in today's growth activities.</summary>
    public int? Purpose { get; set; }

    /// <summary>0-100. Self-reported confidence in current capabilities.</summary>
    public int? Confidence { get; set; }

    /// <summary>Consecutive days (including today) with a check-in.</summary>
    public int Streak { get; set; }

    public DateTime RecordedAt { get; set; }

    /*
     * Navigation properties
     */

    public Person Person { get; set; } = null!;

    public Tenant Tenant { get; set; } = null!;
}
