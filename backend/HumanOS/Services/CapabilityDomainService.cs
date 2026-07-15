using HumanOS.Contracts.Capabilities;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class CapabilityDomainService
{
    private readonly HumanOsDbContext _dbContext;

    public CapabilityDomainService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CapabilityDomainResponse>> GetAsync(
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = languageCode.Trim();

        var domains = await _dbContext.CapabilityDomains
            .AsNoTracking()
            .Include(d => d.Translations)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);

        return domains.Select(d => new CapabilityDomainResponse
        {
            CapabilityDomainId = d.CapabilityDomainId,
            Code = d.Code,
            Name = d.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Name
                ?? d.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? d.Name,
            Description = d.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Description
                ?? d.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Description
                ?? d.Description
        }).ToList();
    }

    public async Task<CapabilityDomainResponse?> GetByCodeAsync(
        string code,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var normalizedLanguageCode = languageCode.Trim();

        var domain = await _dbContext.CapabilityDomains
            .AsNoTracking()
            .Include(d => d.Translations)
            .SingleOrDefaultAsync(
                d => d.Code == normalizedCode,
                cancellationToken);

        if (domain is null)
        {
            return null;
        }

        return new CapabilityDomainResponse
        {
            CapabilityDomainId = domain.CapabilityDomainId,
            Code = domain.Code,
            Name = domain.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Name
                ?? domain.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? domain.Name,
            Description = domain.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Description
                ?? domain.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Description
                ?? domain.Description
        };
    }
}
