namespace HumanOS.Contracts.Tenancy;

/// <summary>Aggregate dashboard stats for one tenant's detail page
/// (2026-10-01) — people count, course counts (owned-by-tenant vs. global,
/// by content language), labs count (from the separate SimulationLabs
/// database, same physical Azure SQL server), and generated media counts.
/// Never includes/deletes global/shared content — purely read-only
/// counts.</summary>
public sealed class TenantDashboardStatsResponse
{
    public int PeopleCount { get; set; }

    public TenantCourseStats Courses { get; set; } = new();

    public TenantLabStats Labs { get; set; } = new();

    public TenantMediaStats Media { get; set; } = new();
}

public sealed class TenantCourseStats
{
    /// <summary>Capabilities + Structured Learning Documents owned by this tenant.</summary>
    public int TotalOwnedByTenant { get; set; }

    /// <summary>Distinct courses with at least one started learning session or recorded learning activity by a person in this tenant.</summary>
    public int TotalConsumedByTenant { get; set; }

    /// <summary>Capabilities + Structured Learning Documents available globally
    /// (shared across every tenant) — shown for context, not tenant-exclusive.</summary>
    public int TotalGlobal { get; set; }

    /// <summary>Content-language breakdown (e.g. "es", "en") of this tenant's
    /// OWN Capabilities only (Structured Learning Documents don't have a
    /// single stored language column).</summary>
    public Dictionary<string, int> ByLanguage { get; set; } = new();
}

public sealed class TenantLabStats
{
    public int OwnedByTenant { get; set; }

    public int Global { get; set; }
}

public sealed class TenantMediaStats
{
    public int ImagesCount { get; set; }

    public int VideosCount { get; set; }
}
