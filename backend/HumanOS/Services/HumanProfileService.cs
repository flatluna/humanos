using HumanOS.Data;
using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class HumanProfileService
{
    private readonly HumanOsDbContext _dbContext;

    public HumanProfileService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HumanProfile?> GetByPersonIdAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.HumanProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                profile => profile.PersonId == personId,
                cancellationToken);
    }

    public async Task<HumanProfile> CreateAsync(
        Guid personId,
        string? missionStatement,
        string? primaryGoal,
        string? learningStyle,
        string? currentLifeStage,
        decimal? weeklyAvailabilityHours,
        decimal? motivationScore,
        decimal? confidenceScore,
        CancellationToken cancellationToken = default)
    {
        var personExists = await _dbContext.People
            .AnyAsync(
                person => person.PersonId == personId,
                cancellationToken);

        if (!personExists)
        {
            throw new KeyNotFoundException(
                "The requested person was not found.");
        }

        var profileExists = await _dbContext.HumanProfiles
            .AnyAsync(
                profile => profile.PersonId == personId,
                cancellationToken);

        if (profileExists)
        {
            throw new InvalidOperationException(
                "A human profile already exists for this person.");
        }

        ValidateValues(
            weeklyAvailabilityHours,
            motivationScore,
            confidenceScore);

        var profile = new HumanProfile
        {
            HumanProfileId = Guid.NewGuid(),
            PersonId = personId,
            MissionStatement = NormalizeOptional(missionStatement),
            PrimaryGoal = NormalizeOptional(primaryGoal),
            LearningStyle = NormalizeOptional(learningStyle),
            CurrentLifeStage = NormalizeOptional(currentLifeStage),
            WeeklyAvailabilityHours = weeklyAvailabilityHours,
            MotivationScore = motivationScore,
            ConfidenceScore = confidenceScore,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _dbContext.HumanProfiles.Add(profile);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return profile;
    }

    public async Task<HumanProfile?> UpdateAsync(
        Guid personId,
        string? missionStatement,
        string? primaryGoal,
        string? learningStyle,
        string? currentLifeStage,
        decimal? weeklyAvailabilityHours,
        decimal? motivationScore,
        decimal? confidenceScore,
        CancellationToken cancellationToken = default)
    {
        ValidateValues(
            weeklyAvailabilityHours,
            motivationScore,
            confidenceScore);

        var profile = await _dbContext.HumanProfiles
            .SingleOrDefaultAsync(
                item => item.PersonId == personId,
                cancellationToken);

        if (profile is null)
        {
            return null;
        }

        profile.MissionStatement =
            NormalizeOptional(missionStatement);

        profile.PrimaryGoal =
            NormalizeOptional(primaryGoal);

        profile.LearningStyle =
            NormalizeOptional(learningStyle);

        profile.CurrentLifeStage =
            NormalizeOptional(currentLifeStage);

        profile.WeeklyAvailabilityHours =
            weeklyAvailabilityHours;

        profile.MotivationScore = motivationScore;
        profile.ConfidenceScore = confidenceScore;
        profile.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return profile;
    }

    private static void ValidateValues(
        decimal? weeklyAvailabilityHours,
        decimal? motivationScore,
        decimal? confidenceScore)
    {
        if (weeklyAvailabilityHours is < 0 or > 168)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weeklyAvailabilityHours),
                "Weekly availability must be between 0 and 168.");
        }

        if (motivationScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(motivationScore),
                "Motivation score must be between 0 and 100.");
        }

        if (confidenceScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidenceScore),
                "Confidence score must be between 0 and 100.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
