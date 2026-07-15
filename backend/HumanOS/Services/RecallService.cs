using HumanOS.Contracts.Responses;
using HumanOS.Data;
using HumanOS.Models.Recall;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class RecallService
{
    private readonly HumanOsDbContext _dbContext;

    public RecallService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RecallAttemptResponse?> CreateAttemptAsync(
        Guid personCapabilityId,
        string recallPrompt,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
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

        var attempt = new RecallAttempt
        {
            RecallAttemptId = Guid.NewGuid(),
            PersonCapabilityId = personCapabilityId,
            RecallPrompt = recallPrompt.Trim(),
            PersonResponse = null,
            RecallScore = null,
            ConfidenceScore = null,
            AssistanceLevel = 0,
            LanguageCode = languageCode.Trim(),
            AttemptedDate = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow
        };

        _dbContext.RecallAttempts.Add(attempt);

        // Update PersonCapability last activity
        personCapability.LastActivityDate = DateTime.UtcNow;
        personCapability.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(attempt);
    }

    public async Task<RecallAttemptResponse?> SubmitResponseAsync(
        Guid recallAttemptId,
        string personResponse,
        decimal? recallScore,
        decimal? confidenceScore,
        int assistanceLevel,
        CancellationToken cancellationToken = default)
    {
        ValidateAssistanceLevel(assistanceLevel);
        ValidateScores(recallScore, confidenceScore);

        var attempt = await _dbContext.RecallAttempts
            .Include(r => r.PersonCapability)
            .SingleOrDefaultAsync(
                r => r.RecallAttemptId == recallAttemptId,
                cancellationToken);

        if (attempt is null)
        {
            return null;
        }

        attempt.PersonResponse = personResponse.Trim();
        attempt.RecallScore = recallScore;
        attempt.ConfidenceScore = confidenceScore;
        attempt.AssistanceLevel = assistanceLevel;

        // Update PersonCapability metrics
        attempt.PersonCapability.LastActivityDate = DateTime.UtcNow;
        attempt.PersonCapability.UpdatedDate = DateTime.UtcNow;

        if (recallScore.HasValue)
        {
            attempt.PersonCapability.RetentionScore = recallScore.Value;
        }

        if (confidenceScore.HasValue)
        {
            attempt.PersonCapability.ConfidenceScore = confidenceScore.Value;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(attempt);
    }

    public async Task<IReadOnlyList<RecallAttemptResponse>> GetHistoryAsync(
        Guid personCapabilityId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var attempts = await _dbContext.RecallAttempts
            .AsNoTracking()
            .Where(r => r.PersonCapabilityId == personCapabilityId)
            .OrderByDescending(r => r.AttemptedDate)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return attempts.Select(MapToResponse).ToList();
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

    private static void ValidateScores(decimal? recallScore, decimal? confidenceScore)
    {
        if (recallScore.HasValue && (recallScore < 0 || recallScore > 100))
        {
            throw new ArgumentOutOfRangeException(
                nameof(recallScore),
                "Recall score must be between 0 and 100.");
        }

        if (confidenceScore.HasValue && (confidenceScore < 0 || confidenceScore > 100))
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidenceScore),
                "Confidence score must be between 0 and 100.");
        }
    }

    private static RecallAttemptResponse MapToResponse(RecallAttempt attempt)
    {
        return new RecallAttemptResponse
        {
            RecallAttemptId = attempt.RecallAttemptId,
            PersonCapabilityId = attempt.PersonCapabilityId,
            RecallPrompt = attempt.RecallPrompt,
            PersonResponse = attempt.PersonResponse,
            RecallScore = attempt.RecallScore,
            ConfidenceScore = attempt.ConfidenceScore,
            AssistanceLevel = attempt.AssistanceLevel,
            LanguageCode = attempt.LanguageCode,
            AttemptedDate = attempt.AttemptedDate,
            CreatedDate = attempt.CreatedDate
        };
    }
}
