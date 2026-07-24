using HumanOS.Contracts.Motivations;
using HumanOS.Data;
using HumanOS.Models.Motivations;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class MotivationService
{
    private readonly HumanOsDbContext _dbContext;

    public MotivationService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MotivationResponse>> GetActiveAsync(
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = languageCode.Trim();

        var motivations = await _dbContext.Motivations
            .AsNoTracking()
            .Include(m => m.Translations)
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);

        return motivations.Select(m => MapToResponse(m, normalizedLanguageCode)).ToList();
    }

    public async Task<IReadOnlyList<PersonMotivationResponse>> GetPersonMotivationsAsync(
        Guid personId,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = languageCode.Trim();

        var personMotivations = await _dbContext.PersonMotivations
            .AsNoTracking()
            .Include(pm => pm.Motivation)
            .ThenInclude(m => m.Translations)
            .Where(pm => pm.PersonId == personId)
            .OrderBy(pm => pm.CreatedDate)
            .ToListAsync(cancellationToken);

        return personMotivations
            .Select(pm => MapPersonMotivationToResponse(pm, normalizedLanguageCode))
            .ToList();
    }

    /// <summary>Replaces the full set of the person's Motivations with the
    /// given catalog Codes (idempotent — unknown codes are ignored, existing
    /// selections not in the new set are removed).</summary>
    public async Task<IReadOnlyList<PersonMotivationResponse>> SetPersonMotivationsAsync(
        Guid personId,
        IReadOnlyCollection<string> motivationCodes,
        CancellationToken cancellationToken = default)
    {
        var personExists = await _dbContext.People
            .AsNoTracking()
            .AnyAsync(p => p.PersonId == personId, cancellationToken);

        if (!personExists)
        {
            throw new KeyNotFoundException($"Person '{personId}' was not found.");
        }

        var normalizedCodes = motivationCodes
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var matchingMotivations = await _dbContext.Motivations
            .Where(m => normalizedCodes.Contains(m.Code))
            .ToListAsync(cancellationToken);

        var existing = await _dbContext.PersonMotivations
            .Where(pm => pm.PersonId == personId)
            .ToListAsync(cancellationToken);

        var matchingIds = matchingMotivations.Select(m => m.MotivationId).ToHashSet();

        var toRemove = existing.Where(pm => !matchingIds.Contains(pm.MotivationId)).ToList();
        if (toRemove.Count > 0)
        {
            _dbContext.PersonMotivations.RemoveRange(toRemove);
        }

        var existingIds = existing.Select(pm => pm.MotivationId).ToHashSet();
        var toAdd = matchingMotivations.Where(m => !existingIds.Contains(m.MotivationId));

        foreach (var motivation in toAdd)
        {
            _dbContext.PersonMotivations.Add(new PersonMotivation
            {
                PersonMotivationId = Guid.NewGuid(),
                PersonId = personId,
                MotivationId = motivation.MotivationId,
                CreatedDate = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetPersonMotivationsAsync(personId, "en", cancellationToken);
    }

    private static MotivationResponse MapToResponse(Motivation motivation, string languageCode)
    {
        return new MotivationResponse
        {
            MotivationId = motivation.MotivationId,
            Code = motivation.Code,
            Name = motivation.Translations?.FirstOrDefault(t => t.LanguageCode == languageCode)?.Name
                ?? motivation.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? motivation.Name,
            IsActive = motivation.IsActive
        };
    }

    private static PersonMotivationResponse MapPersonMotivationToResponse(
        PersonMotivation personMotivation,
        string languageCode)
    {
        return new PersonMotivationResponse
        {
            PersonMotivationId = personMotivation.PersonMotivationId,
            PersonId = personMotivation.PersonId,
            MotivationId = personMotivation.MotivationId,
            MotivationCode = personMotivation.Motivation.Code,
            MotivationName = personMotivation.Motivation.Translations?
                .FirstOrDefault(t => t.LanguageCode == languageCode)?.Name
                ?? personMotivation.Motivation.Translations?
                    .FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? personMotivation.Motivation.Name,
            CreatedDate = personMotivation.CreatedDate
        };
    }
}
