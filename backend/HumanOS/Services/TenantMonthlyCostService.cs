using HumanOS.Agents.Studio;
using HumanOS.Contracts.Tenancy;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>Buckets every LLM-call and media-generation cost for a
/// tenant's own courses into calendar months (2026-10-01) — powers a
/// "spend by month" bar chart. Reuses <see cref="TenantCoursesService"/>
/// to scope to only this tenant's owned Capabilities/Structured Learning
/// Documents, then queries usage/illustration rows directly (rather than
/// going through the per-course cost services) since those don't expose a
/// per-row CreatedDate grouped by month.</summary>
public sealed class TenantMonthlyCostService
{
    // Mirrors StructuredLearningCostService.VideoCostForModel's flat rate
    // (gemini-omni-flash-preview: ~$0.10/sec, 8s clips = $0.80/video;
    // "merged"/local ffmpeg stays free) — duplicated here since that
    // method is private and this service buckets by month instead of
    // per-document.
    private const decimal VideoCostPerVideoUsd = 0.80m;

    private readonly TenantCoursesService _coursesService;
    private readonly HumanOsDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public TenantMonthlyCostService(TenantCoursesService coursesService, HumanOsDbContext dbContext, IConfiguration configuration)
    {
        _coursesService = coursesService;
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<TenantMonthlyCostResponse>> GetMonthlyCostsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var courses = await _coursesService.GetTenantCoursesAsync(tenantId, cancellationToken);
        var capabilityIds = courses.Where(c => c.CourseType == "Capability").Select(c => c.CourseId).ToList();
        var sldIds = courses.Where(c => c.CourseType == "StructuredLearning").Select(c => c.CourseId).ToList();

        var costByMonth = new Dictionary<string, decimal>();

        void AddCost(DateTime createdDate, decimal costUsd)
        {
            var key = createdDate.ToString("yyyy-MM");
            costByMonth[key] = costByMonth.GetValueOrDefault(key) + costUsd;
        }

        if (capabilityIds.Count > 0)
        {
            var capUsageRows = await _dbContext.Set<Models.Capabilities.Graph.CapabilityGenerationUsage>()
                .AsNoTracking()
                .Where(u => capabilityIds.Contains(u.CapabilityId))
                .Select(u => new { u.CreatedDate, u.InputTokens, u.OutputTokens, u.CachedInputTokens, u.ModelName })
                .ToListAsync(cancellationToken);

            foreach (var row in capUsageRows)
            {
                var usage = new List<AgentTokenUsage> { new() { AgentName = string.Empty, ModelName = row.ModelName ?? string.Empty, InputTokens = row.InputTokens, OutputTokens = row.OutputTokens, CachedInputTokens = row.CachedInputTokens } };
                var estimate = TokenCostEstimator.Estimate(usage, 0, _configuration);
                AddCost(row.CreatedDate, estimate.TotalCostUsd);
            }

            var capImageDates = await _dbContext.Set<Models.Capabilities.Graph.CapabilityGraphNodeIllustration>()
                .AsNoTracking()
                .Join(_dbContext.CapabilityGraphNodes, i => i.CapabilityGraphNodeId, n => n.CapabilityGraphNodeId, (i, n) => new { i.CreatedDate, n.CapabilityGraphId })
                .Join(_dbContext.CapabilityGraphs, x => x.CapabilityGraphId, g => g.CapabilityGraphId, (x, g) => new { x.CreatedDate, g.CapabilityId })
                .Where(x => capabilityIds.Contains(x.CapabilityId))
                .Select(x => x.CreatedDate)
                .ToListAsync(cancellationToken);

            var imageRate = ReadDecimal("AgentPricing:PerImage", 0.04m);
            foreach (var createdDate in capImageDates)
            {
                AddCost(createdDate, imageRate);
            }
        }

        if (sldIds.Count > 0)
        {
            var sldUsageRows = await _dbContext.StructuredLearningGenerationUsages
                .AsNoTracking()
                .Where(u => sldIds.Contains(u.StructuredLearningDocumentId))
                .Select(u => new { u.CreatedDate, u.InputTokens, u.OutputTokens, u.CachedInputTokens, u.ModelName })
                .ToListAsync(cancellationToken);

            foreach (var row in sldUsageRows)
            {
                var usage = new List<AgentTokenUsage> { new() { AgentName = string.Empty, ModelName = row.ModelName ?? string.Empty, InputTokens = row.InputTokens, OutputTokens = row.OutputTokens, CachedInputTokens = row.CachedInputTokens } };
                var estimate = TokenCostEstimator.Estimate(usage, 0, _configuration);
                AddCost(row.CreatedDate, estimate.TotalCostUsd);
            }

            var sldIllustrations = await _dbContext.StructuredLearningIllustrations
                .AsNoTracking()
                .Join(
                    _dbContext.StructuredLearningDocumentSections.AsNoTracking().Where(s => sldIds.Contains(s.StructuredLearningDocumentId)),
                    i => i.StructuredLearningDocumentSectionId,
                    s => s.StructuredLearningDocumentSectionId,
                    (i, s) => new { i.CreatedDate, i.MediaType, i.ImageModel })
                .ToListAsync(cancellationToken);

            var imageRate = ReadDecimal("AgentPricing:PerImage", 0.04m);
            foreach (var illustration in sldIllustrations)
            {
                if (illustration.MediaType == "Video")
                {
                    var isMerged = string.Equals(illustration.ImageModel, "merged", StringComparison.OrdinalIgnoreCase);
                    AddCost(illustration.CreatedDate, isMerged ? 0m : VideoCostPerVideoUsd);
                }
                else
                {
                    AddCost(illustration.CreatedDate, imageRate);
                }
            }
        }

        return costByMonth
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new TenantMonthlyCostResponse { YearMonth = kvp.Key, EstimatedCostUsd = Math.Round(kvp.Value, 4) })
            .ToList();
    }

    private decimal ReadDecimal(string key, decimal fallback)
    {
        var raw = _configuration[key];
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
