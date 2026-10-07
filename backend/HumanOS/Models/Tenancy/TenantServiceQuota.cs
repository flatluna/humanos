namespace HumanOS.Models.Tenancy;

/// <summary>
/// One configured quota/limit override for a tenant (2026-10-04) — the
/// Azure/Microsoft-style "service limits" pattern: a generic (TenantId,
/// QuotaKey) row holding a numeric LimitValue, so new quota types (Buddy
/// voice minutes, max courses/month, max voice interactions/month, etc.)
/// never require a new table or migration — just a new <see cref="QuotaKey"/>
/// registered in <see cref="QuotaCatalog"/>. Absence of a row for a given
/// (TenantId, QuotaKey) means "use the platform default" from the catalog,
/// not "unlimited" — see <see cref="Services.TenantQuotaService"/>.
/// </summary>
public sealed class TenantServiceQuota
{
    public Guid TenantServiceQuotaId { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    /// <summary>Stable key matching a <see cref="QuotaCatalog"/> entry, e.g.
    /// "BuddyVoiceMinutesPerMonth".</summary>
    public string QuotaKey { get; set; } = null!;

    /// <summary>The configured limit, in whatever unit the catalog entry
    /// declares (minutes, count, USD, etc.).</summary>
    public decimal LimitValue { get; set; }

    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}
