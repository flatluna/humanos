using HumanOS.Contracts.Tenancy;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>Total Buddy (Realtime voice companion) time/cost for a tenant's
/// own employees (2026-10-01) — powers an admin-dashboard "Buddy usage" card.
/// Buddy minutes are priced via a flat per-minute rate (Azure OpenAI Realtime
/// audio is billed per-audio-token, not per-token-text, so the usual
/// <see cref="TokenCostEstimator"/> doesn't apply) — configure the real rate
/// via "AgentPricing:RealtimePerMinuteUsd"; defaults to $0.045/minute, the
/// midpoint of gpt-realtime-mini's real published ~$0.03-$0.06/minute
/// blended rate for a half-user/half-model-speaking conversation (2026-10-01,
/// corrected down from an earlier $0.15 guess that overstated cost ~3-5x).</summary>
public sealed class TenantBuddyUsageService
{
    private const decimal DefaultRealtimePerMinuteUsd = 0.045m;

    private readonly HumanOsDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public TenantBuddyUsageService(HumanOsDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public async Task<TenantBuddyUsageResponse> GetBuddyUsageAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await (
            from usage in _dbContext.BuddySessionUsages.AsNoTracking()
            join person in _dbContext.People.AsNoTracking() on usage.PersonId equals person.PersonId
            where person.TenantId == tenantId
            select usage.DurationSeconds
        ).ToListAsync(cancellationToken);

        return BuildResponse(rows);
    }

    /// <summary>Buddy (Workplace Companion) time/cost for ONE person
    /// (2026-10-03) \u2014 same flat per-minute pricing as the tenant-wide
    /// rollup, scoped to just this employee's own voice sessions. Powers
    /// the admin "Users" person detail page's "Dashboard" tab.</summary>
    public async Task<TenantBuddyUsageResponse> GetPersonBuddyUsageAsync(Guid personId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.BuddySessionUsages
            .AsNoTracking()
            .Where(u => u.PersonId == personId)
            .Select(u => u.DurationSeconds)
            .ToListAsync(cancellationToken);

        return BuildResponse(rows);
    }

    private TenantBuddyUsageResponse BuildResponse(List<int> durationsSeconds)
    {
        var totalSeconds = durationsSeconds.Sum();
        var perMinuteRate = ReadDecimal("AgentPricing:RealtimePerMinuteUsd", DefaultRealtimePerMinuteUsd);
        var estimatedCost = Math.Round((decimal)totalSeconds / 60m * perMinuteRate, 4);

        return new TenantBuddyUsageResponse
        {
            SessionCount = durationsSeconds.Count,
            TotalDurationSeconds = totalSeconds,
            EstimatedCostUsd = estimatedCost,
        };
    }

    private decimal ReadDecimal(string key, decimal fallback)
    {
        var raw = _configuration[key];
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
