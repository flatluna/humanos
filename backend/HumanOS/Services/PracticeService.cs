using HumanOS.Contracts.Responses;
using HumanOS.Data;
using HumanOS.Models.Practice;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class PracticeService
{
    private readonly HumanOsDbContext _dbContext;

    public PracticeService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PracticeResponse?> RecordAsync(
        Guid personCapabilityId,
        string practiceType,
        int assistanceLevel,
        string? personReflection,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        ValidateAssistanceLevel(assistanceLevel);

        var personCapability = await _dbContext.PersonCapabilities
            .SingleOrDefaultAsync(
                pc => pc.PersonCapabilityId == personCapabilityId,
                cancellationToken);

        if (personCapability is null)
        {
            throw new KeyNotFoundException(
                "The requested person capability was not found.");
        }

        var languageExists = await _dbContext.Languages
            .AnyAsync(
                l => l.LanguageCode == languageCode && l.IsActive,
                cancellationToken);

        if (!languageExists)
        {
            throw new ArgumentException(
                "The specified language is not supported.",
                nameof(languageCode));
        }

        var practice = new CapabilityPractice
        {
            CapabilityPracticeId = Guid.NewGuid(),
            PersonCapabilityId = personCapabilityId,
            PracticeType = practiceType.Trim(),
            AssistanceLevel = assistanceLevel,
            PersonReflection = string.IsNullOrWhiteSpace(personReflection) ? null : personReflection.Trim(),
            LanguageCode = languageCode.Trim(),
            PracticedDate = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow
        };

        _dbContext.CapabilityPractices.Add(practice);

        // Update PersonCapability last activity
        personCapability.LastActivityDate = DateTime.UtcNow;
        personCapability.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PracticeResponse
        {
            CapabilityPracticeId = practice.CapabilityPracticeId,
            PersonCapabilityId = practice.PersonCapabilityId,
            PracticeType = practice.PracticeType,
            AssistanceLevel = practice.AssistanceLevel,
            PersonReflection = practice.PersonReflection,
            LanguageCode = practice.LanguageCode,
            PracticedDate = practice.PracticedDate,
            CreatedDate = practice.CreatedDate
        };
    }

    public async Task<IReadOnlyList<PracticeResponse>> GetByCapabilityCodeAsync(
        Guid personId,
        string capabilityCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = capabilityCode.Trim().ToUpperInvariant();

        var practices = await _dbContext.CapabilityPractices
            .AsNoTracking()
            .Include(p => p.PersonCapability)
            .Where(p => p.PersonCapability.PersonId == personId &&
                        p.PersonCapability.Capability.Code == normalizedCode)
            .OrderByDescending(p => p.PracticedDate)
            .ToListAsync(cancellationToken);

        return practices.Select(MapToResponse).ToList();
    }

    public async Task<IReadOnlyList<PracticeResponse>> GetRecentAsync(
        Guid personId,
        int daysBack = 30,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);

        var practices = await _dbContext.CapabilityPractices
            .AsNoTracking()
            .Include(p => p.PersonCapability)
            .Where(p => p.PersonCapability.PersonId == personId &&
                        p.PracticedDate >= cutoffDate)
            .OrderByDescending(p => p.PracticedDate)
            .ToListAsync(cancellationToken);

        return practices.Select(MapToResponse).ToList();
    }

    private static void ValidateAssistanceLevel(int assistanceLevel)
    {
        if (assistanceLevel < 0 || assistanceLevel > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(assistanceLevel),
                "Assistance level must be between 0 and 5.");
        }
    }

    private static PracticeResponse MapToResponse(CapabilityPractice practice)
    {
        return new PracticeResponse
        {
            CapabilityPracticeId = practice.CapabilityPracticeId,
            PersonCapabilityId = practice.PersonCapabilityId,
            PracticeType = practice.PracticeType,
            AssistanceLevel = practice.AssistanceLevel,
            PersonReflection = practice.PersonReflection,
            LanguageCode = practice.LanguageCode,
            PracticedDate = practice.PracticedDate,
            CreatedDate = practice.CreatedDate
        };
    }
}
