using HumanOS.Data;
using HumanOS.Models.Localization;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class LanguageService
{
    private readonly HumanOsDbContext _dbContext;

    public LanguageService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Language>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Languages
            .AsNoTracking()
            .Where(lang => lang.IsActive)
            .OrderBy(lang => lang.EnglishName)
            .ToListAsync(cancellationToken);
    }

    public async Task<Language?> GetByCodeAsync(string languageCode, CancellationToken cancellationToken = default)
    {
        var normalizedCode = languageCode.Trim();
        return await _dbContext.Languages
            .AsNoTracking()
            .SingleOrDefaultAsync(
                lang => lang.LanguageCode == normalizedCode,
                cancellationToken);
    }

    public async Task<bool> IsActiveAsync(string languageCode, CancellationToken cancellationToken = default)
    {
        var normalizedCode = languageCode.Trim();
        return await _dbContext.Languages
            .AsNoTracking()
            .AnyAsync(
                lang => lang.LanguageCode == normalizedCode && lang.IsActive,
                cancellationToken);
    }
}
