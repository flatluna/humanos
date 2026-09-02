using HumanOS.Contracts.EmployeeScoring;
using HumanOS.Data;
using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Service responsible for persisting Engram impact scores to the database
/// using an efficient batch upsert pattern on the unique key (EmployeeId, CourseId, EngramId).
/// </summary>
public sealed class EngramImpactScorePersistenceService
{
    private readonly HumanOsDbContext _dbContext;

    public EngramImpactScorePersistenceService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Upserts a batch of Engram impact scoring results for a specific employee and course.
    /// Updates existing rows or inserts new ones, then commits with a single SaveChangesAsync.
    /// </summary>
    public async Task SaveScoresAsync(
        Guid employeeId,
        Guid courseId,
        List<EngramScoreResult> scores,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scores);

        if (scores.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;

        // 1. Efficiently load existing rows for this (EmployeeId, CourseId) pair into a dictionary
        var existingEntities = await _dbContext.EngramImpactScores
            .Where(s => s.EmployeeId == employeeId && s.CourseId == courseId)
            .ToListAsync(cancellationToken);

        var existingLookup = existingEntities.ToDictionary(
            s => s.EngramId,
            StringComparer.OrdinalIgnoreCase);

        // 2. Perform in-memory upsert (update existing tracked entities or add new ones)
        foreach (var score in scores)
        {
            if (string.IsNullOrWhiteSpace(score.EngramId))
            {
                continue;
            }

            if (existingLookup.TryGetValue(score.EngramId, out var existing))
            {
                existing.Score = score.Score;
                existing.Tier = score.Tier;
                existing.PrimarySignal = score.PrimarySignal;
                existing.Reason = score.Reason;
                existing.GeneratedAt = now;
            }
            else
            {
                var newEntity = new EngramImpactScore
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employeeId,
                    CourseId = courseId,
                    EngramId = score.EngramId,
                    Score = score.Score,
                    Tier = score.Tier,
                    PrimarySignal = score.PrimarySignal,
                    Reason = score.Reason,
                    GeneratedAt = now
                };

                _dbContext.EngramImpactScores.Add(newEntity);
                existingLookup[score.EngramId] = newEntity;
            }
        }

        // 3. Save all modifications with a single transaction/database roundtrip
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to save EngramImpactScores for Employee '{employeeId}' and Course '{courseId}': {ex.Message}",
                ex);
        }
    }
}
