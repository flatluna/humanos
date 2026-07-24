using HumanOS.Contracts.Capabilities;
using HumanOS.Data;
using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class PersonCapabilityService
{
    private readonly HumanOsDbContext _dbContext;

    public PersonCapabilityService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PersonCapabilityResponse>> GetByPersonAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var personCapabilities = await _dbContext.PersonCapabilities
            .AsNoTracking()
            .Include(pc => pc.Capability)
            .Where(pc => pc.PersonId == personId)
            .OrderBy(pc => pc.Capability.Code)
            .ToListAsync(cancellationToken);

        return personCapabilities.Select(MapToResponse).ToList();
    }

    public async Task<IReadOnlyList<PersonCapabilityResponse>> GetByCapabilityCodeAsync(
        string capabilityCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = capabilityCode.Trim().ToUpperInvariant();

        var personCapabilities = await _dbContext.PersonCapabilities
            .AsNoTracking()
            .Include(pc => pc.Capability)
            .Where(pc => pc.Capability.Code == normalizedCode)
            .OrderBy(pc => pc.PersonId)
            .ToListAsync(cancellationToken);

        return personCapabilities.Select(MapToResponse).ToList();
    }

    public async Task<PersonCapabilityResponse?> StartAsync(
        Guid personId,
        Guid capabilityId,
        int targetLevel = 5,
        string? selfAssessedLevel = null,
        CancellationToken cancellationToken = default)
    {
        ValidateTargetLevel(targetLevel);

        var initialScores = ResolveSelfAssessment(selfAssessedLevel);

        var personCapability = await _dbContext.PersonCapabilities
            .SingleOrDefaultAsync(
                pc => pc.PersonId == personId && pc.CapabilityId == capabilityId,
                cancellationToken);

        if (personCapability is not null)
        {
            throw new InvalidOperationException(
                "This person is already developing this capability.");
        }

        var capabilityExists = await _dbContext.Capabilities
            .AnyAsync(
                c => c.CapabilityId == capabilityId && c.IsActive,
                cancellationToken);

        if (!capabilityExists)
        {
            throw new KeyNotFoundException(
                "The requested capability was not found or is not active.");
        }

        var personExists = await _dbContext.People
            .AnyAsync(
                p => p.PersonId == personId && p.IsActive,
                cancellationToken);

        if (!personExists)
        {
            throw new KeyNotFoundException(
                "The requested person was not found or is not active.");
        }

        var newPersonCapability = new PersonCapability
        {
            PersonCapabilityId = Guid.NewGuid(),
            PersonId = personId,
            CapabilityId = capabilityId,
            CurrentLevel = initialScores.CurrentLevel,
            TargetLevel = targetLevel,
            ProgressPercentage = 0,
            MasteryScore = 0,
            Status = "Active",
            IndependenceLevel = 0,
            RetentionScore = null,
            ConfidenceScore = initialScores.ConfidenceScore,
            KnowledgeScore = initialScores.KnowledgeScore,
            RecallScore = 0,
            ApplicationScore = 0,
            StartedDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _dbContext.PersonCapabilities.Add(newPersonCapability);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Reload with capability details
        var saved = await _dbContext.PersonCapabilities
            .AsNoTracking()
            .Include(pc => pc.Capability)
            .SingleAsync(pc => pc.PersonCapabilityId == newPersonCapability.PersonCapabilityId, cancellationToken);

        return MapToResponse(saved);
    }

    public async Task<PersonCapabilityResponse?> UpdateTargetLevelAsync(
        Guid personCapabilityId,
        int targetLevel,
        CancellationToken cancellationToken = default)
    {
        ValidateTargetLevel(targetLevel);

        var personCapability = await _dbContext.PersonCapabilities
            .Include(pc => pc.Capability)
            .SingleOrDefaultAsync(
                pc => pc.PersonCapabilityId == personCapabilityId,
                cancellationToken);

        if (personCapability is null)
        {
            return null;
        }

        if (personCapability.Status == "Abandoned" || personCapability.Status == "Completed")
        {
            throw new InvalidOperationException(
                $"Cannot update target level for a capability with status '{personCapability.Status}'.");
        }

        personCapability.TargetLevel = targetLevel;
        personCapability.LastActivityDate = DateTime.UtcNow;
        personCapability.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(personCapability);
    }

    public async Task<PersonCapabilityResponse?> PauseAsync(
        Guid personCapabilityId,
        CancellationToken cancellationToken = default)
    {
        var personCapability = await _dbContext.PersonCapabilities
            .Include(pc => pc.Capability)
            .SingleOrDefaultAsync(
                pc => pc.PersonCapabilityId == personCapabilityId,
                cancellationToken);

        if (personCapability is null)
        {
            return null;
        }

        if (personCapability.Status != "Active" && personCapability.Status != "NotStarted")
        {
            throw new InvalidOperationException(
                $"Cannot pause a capability with status '{personCapability.Status}'.");
        }

        personCapability.Status = "Paused";
        personCapability.LastActivityDate = DateTime.UtcNow;
        personCapability.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(personCapability);
    }

    public async Task<PersonCapabilityResponse?> ResumeAsync(
        Guid personCapabilityId,
        CancellationToken cancellationToken = default)
    {
        var personCapability = await _dbContext.PersonCapabilities
            .Include(pc => pc.Capability)
            .SingleOrDefaultAsync(
                pc => pc.PersonCapabilityId == personCapabilityId,
                cancellationToken);

        if (personCapability is null)
        {
            return null;
        }

        if (personCapability.Status != "Paused")
        {
            throw new InvalidOperationException(
                $"Cannot resume a capability with status '{personCapability.Status}'.");
        }

        personCapability.Status = "Active";
        personCapability.LastActivityDate = DateTime.UtcNow;
        personCapability.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(personCapability);
    }

    private static void ValidateTargetLevel(int targetLevel)
    {
        if (targetLevel < 0 || targetLevel > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetLevel),
                "Target level must be between 0 and 5.");
        }
    }

    /// <summary>
    /// Translates the individual onboarding survey's self-reported starting
    /// point ('Beginner' / 'Intermediate' / 'Advanced') into initial values
    /// for the real <see cref="PersonCapability"/> tracking fields. A null
    /// or unrecognized value falls back to the original "unknown" defaults
    /// (current behavior for the employee/organization flow, which does not
    /// go through the survey).
    /// </summary>
    private static (int CurrentLevel, decimal? ConfidenceScore, int KnowledgeScore) ResolveSelfAssessment(
        string? selfAssessedLevel)
    {
        if (string.IsNullOrWhiteSpace(selfAssessedLevel))
        {
            return (0, null, 0);
        }

        return selfAssessedLevel.Trim().ToLowerInvariant() switch
        {
            "beginner" => (0, 20m, 10),
            "intermediate" => (1, 50m, 40),
            "advanced" => (2, 80m, 70),
            _ => throw new ArgumentException(
                "Self-assessed level must be one of 'Beginner', 'Intermediate', 'Advanced'.",
                nameof(selfAssessedLevel))
        };
    }

    private static PersonCapabilityResponse MapToResponse(PersonCapability pc)
    {
        return new PersonCapabilityResponse
        {
            PersonCapabilityId = pc.PersonCapabilityId,
            PersonId = pc.PersonId,
            CapabilityId = pc.CapabilityId,
            CapabilityCode = pc.Capability.Code,
            CapabilityName = pc.Capability.Name,
            CurrentLevel = pc.CurrentLevel,
            TargetLevel = pc.TargetLevel,
            ProgressPercentage = Math.Min(100, Math.Max(0, pc.ProgressPercentage)),
            MasteryScore = pc.MasteryScore,
            IndependenceLevel = pc.IndependenceLevel,
            RetentionScore = pc.RetentionScore,
            ConfidenceScore = pc.ConfidenceScore,
            KnowledgeScore = pc.KnowledgeScore,
            RecallScore = pc.RecallScore,
            ApplicationScore = pc.ApplicationScore,
            Status = pc.Status,
            StartedDate = pc.StartedDate,
            LastActivityDate = pc.LastActivityDate,
            CreatedDate = pc.CreatedDate,
            UpdatedDate = pc.UpdatedDate
        };
    }
}
