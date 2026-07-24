using HumanOS.Contracts.Capabilities;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class SubjectService
{
    private readonly HumanOsDbContext _dbContext;

    public SubjectService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SubjectResponse>> GetAsync(
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = languageCode.Trim();

        var subjects = await _dbContext.Subjects
            .AsNoTracking()
            .Include(s => s.Translations)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return subjects.Select(s => new SubjectResponse
        {
            SubjectId = s.SubjectId,
            Code = s.Code,
            Name = s.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Name
                ?? s.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? s.Name,
            Description = s.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Description
                ?? s.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Description
                ?? s.Description
        }).ToList();
    }

    public async Task<SubjectResponse?> GetByCodeAsync(
        string code,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.Trim().ToLowerInvariant();
        var normalizedLanguageCode = languageCode.Trim();

        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .Include(s => s.Translations)
            .SingleOrDefaultAsync(
                s => s.Code == normalizedCode,
                cancellationToken);

        if (subject is null)
        {
            return null;
        }

        return new SubjectResponse
        {
            SubjectId = subject.SubjectId,
            Code = subject.Code,
            Name = subject.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Name
                ?? subject.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? subject.Name,
            Description = subject.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Description
                ?? subject.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Description
                ?? subject.Description
        };
    }
}
