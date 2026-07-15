using HumanOS.Contracts.Responses;
using HumanOS.Data;
using HumanOS.Models.Assessments;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class AssessmentService
{
    private readonly HumanOsDbContext _dbContext;

    public AssessmentService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AssessmentResponse>> GetActiveByCapabilityCodeAsync(
        string capabilityCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = capabilityCode.Trim().ToUpperInvariant();

        var assessments = await _dbContext.Assessments
            .AsNoTracking()
            .Include(a => a.Capability)
            .Where(a => a.Capability.Code == normalizedCode && a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        return assessments.Select(MapToResponse).ToList();
    }

    public async Task<AssessmentResponse?> GetByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default)
    {
        var assessment = await _dbContext.Assessments
            .AsNoTracking()
            .Include(a => a.Capability)
            .SingleOrDefaultAsync(
                a => a.AssessmentId == assessmentId,
                cancellationToken);

        if (assessment is null)
        {
            return null;
        }

        return MapToResponse(assessment);
    }

    public async Task<AssessmentAttemptResponse?> StartAttemptAsync(
        Guid assessmentId,
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var assessmentExists = await _dbContext.Assessments
            .AnyAsync(
                a => a.AssessmentId == assessmentId && a.IsActive,
                cancellationToken);

        if (!assessmentExists)
        {
            throw new KeyNotFoundException(
                "The requested assessment was not found or is not active.");
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

        var attempt = new AssessmentAttempt
        {
            AssessmentAttemptId = Guid.NewGuid(),
            AssessmentId = assessmentId,
            PersonId = personId,
            Score = null,
            AssistanceLevel = 0,
            StartedDate = DateTime.UtcNow,
            CompletedDate = null,
            CreatedDate = DateTime.UtcNow
        };

        _dbContext.AssessmentAttempts.Add(attempt);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapAttemptToResponse(attempt);
    }

    public async Task<AssessmentAttemptResponse?> CompleteAttemptAsync(
        Guid attemptId,
        decimal score,
        int assistanceLevel,
        CancellationToken cancellationToken = default)
    {
        ValidateScore(score);
        ValidateAssistanceLevel(assistanceLevel);

        var attempt = await _dbContext.AssessmentAttempts
            .Include(a => a.Assessment)
            .SingleOrDefaultAsync(
                a => a.AssessmentAttemptId == attemptId,
                cancellationToken);

        if (attempt is null)
        {
            return null;
        }

        if (attempt.CompletedDate.HasValue)
        {
            throw new InvalidOperationException(
                "This assessment attempt has already been completed.");
        }

        attempt.Score = score;
        attempt.AssistanceLevel = assistanceLevel;
        attempt.CompletedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapAttemptToResponse(attempt);
    }

    public async Task<IReadOnlyList<AssessmentAttemptResponse>> GetPersonAttemptsAsync(
        Guid personId,
        Guid? assessmentId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AssessmentAttempts
            .AsNoTracking()
            .Where(a => a.PersonId == personId);

        if (assessmentId.HasValue)
        {
            query = query.Where(a => a.AssessmentId == assessmentId.Value);
        }

        var attempts = await query
            .OrderByDescending(a => a.StartedDate)
            .ToListAsync(cancellationToken);

        return attempts.Select(MapAttemptToResponse).ToList();
    }

    private static void ValidateScore(decimal score)
    {
        if (score < 0 || score > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(score),
                "Assessment score must be between 0 and 100.");
        }
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

    private static AssessmentResponse MapToResponse(Assessment assessment)
    {
        return new AssessmentResponse
        {
            AssessmentId = assessment.AssessmentId,
            CapabilityId = assessment.CapabilityId,
            CapabilityCode = assessment.Capability.Code,
            Name = assessment.Name,
            Description = assessment.Description,
            AssessmentType = assessment.AssessmentType,
            PassingScore = assessment.PassingScore,
            MaxScore = assessment.MaxScore,
            IsActive = assessment.IsActive
        };
    }

    private static AssessmentAttemptResponse MapAttemptToResponse(AssessmentAttempt attempt)
    {
        return new AssessmentAttemptResponse
        {
            AssessmentAttemptId = attempt.AssessmentAttemptId,
            AssessmentId = attempt.AssessmentId,
            PersonId = attempt.PersonId,
            Score = attempt.Score,
            AssistanceLevel = attempt.AssistanceLevel,
            StartedDate = attempt.StartedDate,
            CompletedDate = attempt.CompletedDate,
            CreatedDate = attempt.CreatedDate
        };
    }
}
