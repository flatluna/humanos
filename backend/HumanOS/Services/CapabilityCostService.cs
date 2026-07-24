using HumanOS.Agents.Studio;
using HumanOS.Contracts.Studio;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>
/// Reads persisted <see cref="Models.Capabilities.Graph.CapabilityGenerationUsage"/>
/// rows + illustration counts and turns them into cost-dashboard responses,
/// reusing <see cref="TokenCostEstimator"/> for the USD estimate.
/// </summary>
public sealed class CapabilityCostService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public CapabilityCostService(HumanOsDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    /// <summary>List view: one row per Capability that has a generated graph,
    /// with aggregated token/image totals and an estimated cost. When
    /// <paramref name="date"/> is given, only capabilities whose earliest
    /// usage row (<see cref="CapabilityCostSummaryResponse.GeneratedDate"/>)
    /// falls on that UTC calendar day are returned (2026-07-23).</summary>
    public async Task<IReadOnlyList<CapabilityCostSummaryResponse>> GetSummariesAsync(
        DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        var capabilities = await _dbContext.Capabilities
            .AsNoTracking()
            .Where(c => c.CapabilityGraph != null)
            .Select(c => new { c.CapabilityId, c.Name })
            .ToListAsync(cancellationToken);

        var usageTotals = await _dbContext.CapabilityGenerationUsages
            .AsNoTracking()
            .GroupBy(u => u.CapabilityId)
            .Select(g => new
            {
                CapabilityId = g.Key,
                InputTokens = g.Sum(u => u.InputTokens),
                OutputTokens = g.Sum(u => u.OutputTokens),
                CachedInputTokens = g.Sum(u => u.CachedInputTokens),
            })
            .ToDictionaryAsync(x => x.CapabilityId, cancellationToken);

        // Grouped by ModelName too (2026-07-23) so TokenCostEstimator can
        // apply each model's own per-token rate instead of one flat rate —
        // see AgentTokenUsage.ModelName's doc comment for why this matters.
        var usageByModel = await _dbContext.CapabilityGenerationUsages
            .AsNoTracking()
            .GroupBy(u => new { u.CapabilityId, u.ModelName })
            .Select(g => new
            {
                g.Key.CapabilityId,
                g.Key.ModelName,
                InputTokens = g.Sum(u => u.InputTokens),
                OutputTokens = g.Sum(u => u.OutputTokens),
                CachedInputTokens = g.Sum(u => u.CachedInputTokens),
            })
            .ToListAsync(cancellationToken);

        var usageByModelLookup = usageByModel
            .GroupBy(u => u.CapabilityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var imageCounts = await _dbContext.CapabilityGraphNodeIllustrations
            .AsNoTracking()
            .GroupBy(i => i.CapabilityGraphNode!.CapabilityGraph!.CapabilityId)
            .Select(g => new { CapabilityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CapabilityId, x => x.Count, cancellationToken);

        var generatedDates = await _dbContext.CapabilityGenerationUsages
            .AsNoTracking()
            .GroupBy(u => u.CapabilityId)
            .Select(g => new { CapabilityId = g.Key, GeneratedDate = g.Min(u => u.CreatedDate) })
            .ToDictionaryAsync(x => x.CapabilityId, x => x.GeneratedDate, cancellationToken);

        var results = new List<CapabilityCostSummaryResponse>(capabilities.Count);
        foreach (var capability in capabilities)
        {
            usageTotals.TryGetValue(capability.CapabilityId, out var totals);
            imageCounts.TryGetValue(capability.CapabilityId, out var imageCount);
            generatedDates.TryGetValue(capability.CapabilityId, out var generatedDate);

            var tokenUsage = usageByModelLookup.TryGetValue(capability.CapabilityId, out var modelRows)
                ? modelRows.Select(m => new AgentTokenUsage
                {
                    AgentName = "Total",
                    ModelName = m.ModelName ?? string.Empty,
                    InputTokens = m.InputTokens,
                    OutputTokens = m.OutputTokens,
                    CachedInputTokens = m.CachedInputTokens,
                }).ToList()
                : [];

            var estimate = TokenCostEstimator.Estimate(tokenUsage, imageCount, _configuration);

            results.Add(new CapabilityCostSummaryResponse
            {
                CapabilityId = capability.CapabilityId,
                CapabilityName = capability.Name,
                InputTokens = totals?.InputTokens ?? 0,
                OutputTokens = totals?.OutputTokens ?? 0,
                CachedInputTokens = totals?.CachedInputTokens ?? 0,
                ImagesGeneratedCount = imageCount,
                EstimatedCostUsd = estimate.TotalCostUsd,
                GeneratedDate = generatedDate == default ? null : generatedDate,
            });
        }

        var filtered = date is null
            ? results
            : results.Where(r => r.GeneratedDate.HasValue && DateOnly.FromDateTime(r.GeneratedDate.Value) == date.Value).ToList();

        return filtered
            .OrderByDescending(r => r.InputTokens + r.OutputTokens)
            .ToList();
    }

    /// <summary>Detail view for one Capability's expanded card: per-section
    /// (chapter/node) token breakdown plus totals and estimated cost.</summary>
    public async Task<CapabilityCostDetailResponse?> GetDetailAsync(
        Guid capabilityId,
        CancellationToken cancellationToken = default)
    {
        var capability = await _dbContext.Capabilities
            .AsNoTracking()
            .Where(c => c.CapabilityId == capabilityId)
            .Select(c => new { c.CapabilityId, c.Name })
            .SingleOrDefaultAsync(cancellationToken);

        if (capability is null) return null;

        var usageRows = await _dbContext.CapabilityGenerationUsages
            .AsNoTracking()
            .Where(u => u.CapabilityId == capabilityId)
            .ToListAsync(cancellationToken);

        var imagesCount = await _dbContext.CapabilityGraphNodeIllustrations
            .AsNoTracking()
            .CountAsync(i => i.CapabilityGraphNode!.CapabilityGraph!.CapabilityId == capabilityId, cancellationToken);

        var sections = usageRows
            .GroupBy(u => string.IsNullOrWhiteSpace(u.SectionLabel) ? "(sin sección)" : u.SectionLabel!)
            .Select(g =>
            {
                var rows = g.ToList();
                var sectionTokenUsage = rows
                    .Select(u => new AgentTokenUsage
                    {
                        AgentName = u.AgentName,
                        ModelName = u.ModelName ?? string.Empty,
                        InputTokens = u.InputTokens,
                        OutputTokens = u.OutputTokens,
                        CachedInputTokens = u.CachedInputTokens,
                    })
                    .ToList();

                // Images aren't attributed to a section, only counted once
                // in the capability-level total — pass 0 here to avoid
                // double-counting image cost across sections.
                var sectionEstimate = TokenCostEstimator.Estimate(sectionTokenUsage, illustrationsGeneratedCount: 0, _configuration);

                return new CapabilityCostSectionResponse
                {
                    SectionLabel = g.Key,
                    Agents = string.Join(", ", rows.Select(u => u.AgentName).Distinct()),
                    Models = string.Join(", ", rows
                        .Select(u => u.ModelName)
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .Distinct()),
                    InputTokens = rows.Sum(u => u.InputTokens),
                    OutputTokens = rows.Sum(u => u.OutputTokens),
                    CachedInputTokens = rows.Sum(u => u.CachedInputTokens),
                    EstimatedCostUsd = sectionEstimate.TotalCostUsd,
                };
            })
            .OrderByDescending(s => s.InputTokens + s.OutputTokens)
            .ToList();

        var tokenUsage = usageRows
            .Select(u => new AgentTokenUsage
            {
                AgentName = u.AgentName,
                ModuleId = u.SectionLabel,
                ModelName = u.ModelName ?? string.Empty,
                InputTokens = u.InputTokens,
                OutputTokens = u.OutputTokens,
                CachedInputTokens = u.CachedInputTokens,
            })
            .ToList();

        var estimate = TokenCostEstimator.Estimate(tokenUsage, imagesCount, _configuration);

        return new CapabilityCostDetailResponse
        {
            CapabilityId = capability.CapabilityId,
            CapabilityName = capability.Name,
            Sections = sections,
            InputTokens = tokenUsage.Sum(u => (long)u.InputTokens),
            OutputTokens = tokenUsage.Sum(u => (long)u.OutputTokens),
            CachedInputTokens = tokenUsage.Sum(u => (long)u.CachedInputTokens),
            ImagesGeneratedCount = imagesCount,
            EstimatedCostUsd = estimate.TotalCostUsd,
        };
    }
}
