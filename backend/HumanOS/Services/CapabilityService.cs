using HumanOS.Contracts.Capabilities;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class CapabilityService
{
    private readonly HumanOsDbContext _dbContext;

    public CapabilityService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CapabilityResponse>> GetActiveAsync(
        string languageCode,
        string? domainCode = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = languageCode.Trim();
        var query = _dbContext.Capabilities
            .AsNoTracking()
            .Include(c => c.CapabilityDomain)
            .Include(c => c.Translations)
            .Include(c => c.Levels)
                .ThenInclude(l => l.Modules)
            .Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(domainCode))
        {
            var normalizedDomainCode = domainCode.Trim().ToUpperInvariant();
            query = query.Where(c => c.CapabilityDomain.Code == normalizedDomainCode);
        }

        var capabilities = await query
            .OrderBy(c => c.CapabilityDomain.Code)
            .ThenBy(c => c.Code)
            .ToListAsync(cancellationToken);

        return capabilities.Select(c => new CapabilityResponse
        {
            CapabilityId = c.CapabilityId,
            CapabilityDomainId = c.CapabilityDomainId,
            DomainCode = c.CapabilityDomain.Code,
            Code = c.Code,
            Name = c.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Name
                ?? c.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? c.Name,
            Description = c.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Description
                ?? c.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Description
                ?? c.Description,
            IsActive = c.IsActive,
            LevelCount = c.Levels.Count,
            ModuleCount = c.Levels.Sum(l => l.Modules.Count),
            Levels = [.. c.Levels.OrderBy(l => l.SortOrder).Select(l => l.Layer)],
            CreatedDate = c.CreatedDate,
            UpdatedDate = c.UpdatedDate
        }).ToList();
    }

    public async Task<CapabilityResponse?> GetByCodeAsync(
        string code,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var normalizedLanguageCode = languageCode.Trim();

        var capability = await _dbContext.Capabilities
            .AsNoTracking()
            .Include(c => c.CapabilityDomain)
            .Include(c => c.Translations)
            .SingleOrDefaultAsync(
                c => c.Code == normalizedCode,
                cancellationToken);

        if (capability is null)
        {
            return null;
        }

        return new CapabilityResponse
        {
            CapabilityId = capability.CapabilityId,
            CapabilityDomainId = capability.CapabilityDomainId,
            DomainCode = capability.CapabilityDomain.Code,
            Code = capability.Code,
            Name = capability.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Name
                ?? capability.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? capability.Name,
            Description = capability.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Description
                ?? capability.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Description
                ?? capability.Description,
            IsActive = capability.IsActive
        };
    }

    public async Task<CapabilityResponse?> GetByIdAsync(
        Guid capabilityId,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = languageCode.Trim();

        var capability = await _dbContext.Capabilities
            .AsNoTracking()
            .Include(c => c.CapabilityDomain)
            .Include(c => c.Translations)
            .SingleOrDefaultAsync(
                c => c.CapabilityId == capabilityId,
                cancellationToken);

        if (capability is null)
        {
            return null;
        }

        return new CapabilityResponse
        {
            CapabilityId = capability.CapabilityId,
            CapabilityDomainId = capability.CapabilityDomainId,
            DomainCode = capability.CapabilityDomain.Code,
            Code = capability.Code,
            Name = capability.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Name
                ?? capability.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? capability.Name,
            Description = capability.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Description
                ?? capability.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Description
                ?? capability.Description,
            IsActive = capability.IsActive
        };
    }

    /// <summary>
    /// Full read-only content (levels + modules + scripts + metrics) for
    /// the "view real generated content" screen. Unlike GetByIdAsync,
    /// this does NOT apply translations (content is authored in a single
    /// language today) and includes the full per-module Script text.
    /// </summary>
    public async Task<CapabilityContentResponse?> GetContentByIdAsync(
        Guid capabilityId,
        CancellationToken cancellationToken = default)
    {
        var capability = await _dbContext.Capabilities
            .AsNoTracking()
            .Include(c => c.Levels)
                .ThenInclude(l => l.Modules)
                    .ThenInclude(m => m.Metrics)
            .SingleOrDefaultAsync(c => c.CapabilityId == capabilityId, cancellationToken);

        if (capability is null)
        {
            return null;
        }

        return new CapabilityContentResponse
        {
            CapabilityId = capability.CapabilityId,
            Code = capability.Code,
            Name = capability.Name,
            Description = capability.Description,
            Levels = [.. capability.Levels
                .OrderBy(l => l.SortOrder)
                .Select(l => new CapabilityContentLevel
                {
                    CapabilityLevelId = l.CapabilityLevelId,
                    Layer = l.Layer,
                    Title = l.Title,
                    HumanTransformation = l.HumanTransformation,
                    Modules = [.. l.Modules
                        .OrderBy(m => m.SortOrder)
                        .Select(m => new CapabilityContentModule
                        {
                            CapabilityModuleId = m.CapabilityModuleId,
                            SortOrder = m.SortOrder,
                            Title = m.Title,
                            Description = m.Description,
                            Type = m.Type,
                            Script = m.Script,
                            MetricRationale = m.MetricRationale,
                            Metrics = [.. m.Metrics.Select(mm => mm.Metric)]
                        })]
                })]
        };
    }

    /// <summary>
    /// Permanently deletes a capability and all its content (levels,
    /// modules, metrics, knowledge chunks). CapabilityKnowledgeChunk's FKs
    /// are Restrict (not Cascade — see CapabilityKnowledgeChunkConfiguration,
    /// avoids a multiple-cascade-paths error), so those rows must be
    /// deleted explicitly first; Capability -> Levels -> Modules -> Metrics
    /// all cascade at the database level once the Capability row itself is
    /// deleted. Returns false if no matching capability was found.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid capabilityId, CancellationToken cancellationToken = default)
    {
        await _dbContext.CapabilityKnowledgeChunks
            .Where(k => k.CapabilityId == capabilityId)
            .ExecuteDeleteAsync(cancellationToken);

        var rowsDeleted = await _dbContext.Capabilities
            .Where(c => c.CapabilityId == capabilityId)
            .ExecuteDeleteAsync(cancellationToken);

        return rowsDeleted > 0;
    }
}
