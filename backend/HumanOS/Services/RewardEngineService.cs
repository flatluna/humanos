using HumanOS.Contracts.Gamification;
using HumanOS.Data;
using HumanOS.Models.Gamification;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Service responsible for granting neuro-science based gamification rewards when a student masters an Engram.
/// Calculates base points, difficulty multipliers, memory effort multipliers, and variable surprise bonuses.
/// Ensures strict idempotency per (PersonId, CourseId, EngramId) and updates the student's PlayerProfile.
/// </summary>
public class RewardEngineService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly Func<int>? _randomRollProvider;

    public RewardEngineService(HumanOsDbContext dbContext)
        : this(dbContext, null)
    {
    }

    /// <summary>
    /// Internal / test constructor to allow deterministic random roll injection.
    /// </summary>
    public RewardEngineService(HumanOsDbContext dbContext, Func<int>? randomRollProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _randomRollProvider = randomRollProvider;
    }

    /// <summary>
    /// Grants an engram reward if not already earned, updates player points/level, and records the frozen ledger entry.
    /// </summary>
    public async Task<RewardResult> GrantEngramRewardAsync(
        Guid personId,
        Guid courseId,
        string engramId,
        string sourceType,
        int? engramDifficultyLevel,
        double? memoryRecallPct,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engramId);

        // 1. IDEMPOTENCIA (crítico):
        // Check if an award already exists for (PersonId, CourseId, EngramId)
        var existingEntry = await _dbContext.RewardLedgerEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PersonId == personId && x.CourseId == courseId && x.EngramId == engramId, ct);

        if (existingEntry != null)
        {
            var existingProfile = await _dbContext.PlayerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PersonId == personId, ct);

            var existingImpactScore = await _dbContext.EngramImpactScores
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.EmployeeId == personId && x.CourseId == courseId && x.EngramId == engramId, ct);

            return new RewardResult
            {
                AlreadyEarned = true,
                Tier = existingImpactScore?.Tier ?? "Foundational",
                PointsBase = existingEntry.PointsBase,
                DifficultyMultiplier = existingEntry.DifficultyMultiplier,
                MemoryEffortMultiplier = existingEntry.MemoryEffortMultiplier,
                SurpriseMultiplier = existingEntry.SurpriseMultiplier,
                PointsAwarded = existingEntry.PointsAwarded,
                WasSurprise = existingEntry.WasSurprise,
                SurpriseType = existingEntry.SurpriseType ?? "NONE",
                NewTotalPoints = existingProfile?.TotalPoints ?? 0,
                NewLevel = existingProfile?.CurrentLevel ?? 1,
                NewLevelName = existingProfile?.CurrentLevelName ?? "Neurona",
                LeveledUp = false
            };
        }

        // 2. PUNTOS BASE:
        // Query impact score for (personId, courseId, engramId)
        var impactScore = await _dbContext.EngramImpactScores
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmployeeId == personId && x.CourseId == courseId && x.EngramId == engramId, ct);

        int pointsBase = impactScore?.Score ?? 50;
        int impactScoreAtTime = pointsBase;

        // 3. MULTIPLICADOR DE DIFICULTAD:
        decimal difficultyMultiplier = engramDifficultyLevel switch
        {
            1 => 1.0m,
            2 => 1.1m,
            3 => 1.2m,
            4 => 1.35m,
            5 => 1.5m,
            _ => 1.0m
        };

        // 4. MULTIPLICADOR DE ESFUERZO-MEMORIA:
        decimal memoryEffortMultiplier = 1.0m;
        if (memoryRecallPct.HasValue)
        {
            double recall = memoryRecallPct.Value;
            if (recall >= 80.0)
            {
                memoryEffortMultiplier = 1.0m;
            }
            else if (recall >= 50.0)
            {
                memoryEffortMultiplier = 1.3m;
            }
            else if (recall >= 25.0)
            {
                memoryEffortMultiplier = 1.8m;
            }
            else
            {
                memoryEffortMultiplier = 2.5m;
            }
        }

        // 5. SORPRESA (aleatoria 0-100):
        int roll = _randomRollProvider != null
            ? _randomRollProvider()
            : Random.Shared.Next(0, 101);

        decimal surpriseMultiplier;
        string surpriseType;
        bool wasSurprise;

        if (roll >= 99)
        {
            surpriseMultiplier = 3.0m;
            surpriseType = "JACKPOT";
            wasSurprise = true;
        }
        else if (roll >= 93)
        {
            surpriseMultiplier = 2.0m;
            surpriseType = "DOUBLE";
            wasSurprise = true;
        }
        else if (roll >= 75)
        {
            surpriseMultiplier = 1.5m;
            surpriseType = "BONUS";
            wasSurprise = true;
        }
        else
        {
            surpriseMultiplier = 1.0m;
            surpriseType = "NONE";
            wasSurprise = false;
        }

        // 6. TOTAL:
        decimal rawPoints = (decimal)pointsBase * difficultyMultiplier * memoryEffortMultiplier * surpriseMultiplier;
        int pointsAwarded = (int)Math.Round(rawPoints, MidpointRounding.AwayFromZero);

        // 7. GUARDAR en RewardLedgerEntry (congelado):
        var ledgerEntry = new RewardLedgerEntry
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            CourseId = courseId,
            EngramId = engramId,
            PointsBase = pointsBase,
            DifficultyMultiplier = difficultyMultiplier,
            MemoryEffortMultiplier = memoryEffortMultiplier,
            SurpriseMultiplier = surpriseMultiplier,
            PointsAwarded = pointsAwarded,
            WasSurprise = wasSurprise,
            SurpriseType = surpriseType,
            ImpactScoreAtTime = impactScoreAtTime,
            Reason = $"Mastered {engramId} (impact {impactScoreAtTime}) — {surpriseType} bonus",
            SourceType = sourceType,
            EarnedAt = DateTime.UtcNow
        };
        _dbContext.RewardLedgerEntries.Add(ledgerEntry);

        // 8. ACTUALIZAR PlayerProfile (crear si no existe):
        var profile = await _dbContext.PlayerProfiles
            .FirstOrDefaultAsync(x => x.PersonId == personId, ct);

        if (profile == null)
        {
            profile = new PlayerProfile
            {
                Id = Guid.NewGuid(),
                PersonId = personId,
                TotalPoints = 0,
                CurrentLevel = 1,
                CurrentLevelName = "Neurona",
                CurrentStreakDays = 0,
                LongestStreakDays = 0,
                StreakFreezesAvailable = 1,
                LastActivityDate = DateTime.UtcNow,
                TotalEngramsMastered = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.PlayerProfiles.Add(profile);
        }

        int previousLevel = profile.CurrentLevel;
        profile.TotalPoints += pointsAwarded;
        profile.TotalEngramsMastered += 1;
        profile.LastActivityDate = DateTime.UtcNow;

        var (newLevel, newLevelName) = CalculateLevel(profile.TotalPoints);
        profile.CurrentLevel = newLevel;
        profile.CurrentLevelName = newLevelName;
        profile.UpdatedAt = DateTime.UtcNow;

        bool leveledUp = newLevel > previousLevel;

        // 9. Guardar TODO en una sola transacción:
        await _dbContext.SaveChangesAsync(ct);

        // 10. Devolver RewardResult:
        return new RewardResult
        {
            AlreadyEarned = false,
            Tier = impactScore?.Tier ?? "Foundational",
            PointsBase = pointsBase,
            DifficultyMultiplier = difficultyMultiplier,
            MemoryEffortMultiplier = memoryEffortMultiplier,
            SurpriseMultiplier = surpriseMultiplier,
            PointsAwarded = pointsAwarded,
            WasSurprise = wasSurprise,
            SurpriseType = surpriseType,
            NewTotalPoints = profile.TotalPoints,
            NewLevel = profile.CurrentLevel,
            NewLevelName = profile.CurrentLevelName,
            LeveledUp = leveledUp
        };
    }

    /// <summary>
    /// Calculates the level and level name based on total accumulated points.
    /// Bands:
    /// 0 - 499     -> Level 1 "Neurona"
    /// 500 - 1499  -> Level 2 "Sinapsis"
    /// 1500 - 3499 -> Level 3 "Circuito"
    /// 3500 - 6999 -> Level 4 "Red Neuronal"
    /// 7000+       -> Level 5 "Mente Maestra"
    /// </summary>
    public static (int Level, string LevelName) CalculateLevel(int totalPoints)
    {
        if (totalPoints >= 7000)
        {
            return (5, "Mente Maestra");
        }

        if (totalPoints >= 3500)
        {
            return (4, "Red Neuronal");
        }

        if (totalPoints >= 1500)
        {
            return (3, "Circuito");
        }

        if (totalPoints >= 500)
        {
            return (2, "Sinapsis");
        }

        return (1, "Neurona");
    }
}
