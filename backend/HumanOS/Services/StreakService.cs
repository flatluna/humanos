using HumanOS.Contracts.Gamification;
using HumanOS.Data;
using HumanOS.Models.Gamification;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Service responsible for managing daily student learning streaks and freeze forgiveness.
/// Uses strict calendar day (.Date) comparisons in UTC to avoid time-of-day edge cases.
/// </summary>
public class StreakService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly Func<DateTime>? _utcNowProvider;

    public StreakService(HumanOsDbContext dbContext)
        : this(dbContext, null)
    {
    }

    /// <summary>
    /// Internal / test constructor to allow deterministic clock injection.
    /// </summary>
    public StreakService(HumanOsDbContext dbContext, Func<DateTime>? utcNowProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _utcNowProvider = utcNowProvider;
    }

    /// <summary>
    /// Registers a learning activity for the given person and updates their streak state.
    /// </summary>
    public async Task<StreakResult> RegisterActivityAsync(
        Guid personId,
        CancellationToken ct = default)
    {
        var now = _utcNowProvider != null ? _utcNowProvider() : DateTime.UtcNow;
        var today = now.Date;

        // 1. Load (or create if not found) PlayerProfile
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
                LastActivityDate = null,
                TotalEngramsMastered = 0,
                UpdatedAt = now
            };
            _dbContext.PlayerProfiles.Add(profile);
        }

        var lastDate = profile.LastActivityDate?.Date;
        string streakChanged;
        bool isNewRecord = false;
        int previousLongest = profile.LongestStreakDays;

        // 2 & 3. Cases based on .Date comparison
        if (lastDate == null || profile.CurrentStreakDays == 0)
        {
            // Case a: First activity ever (or initial uncounted state)
            profile.CurrentStreakDays = 1;
            profile.LongestStreakDays = Math.Max(profile.LongestStreakDays, 1);
            streakChanged = "STARTED";
            isNewRecord = profile.LongestStreakDays <= 1;

            profile.LastActivityDate = now;
            profile.UpdatedAt = now;
        }
        else if (lastDate.Value == today)
        {
            // Case b: Already recorded today
            streakChanged = "ALREADY_COUNTED_TODAY";
            isNewRecord = false;

            profile.UpdatedAt = now;
        }
        else if (lastDate.Value == today.AddDays(-1))
        {
            // Case c: Yesterday -> Streak continues
            profile.CurrentStreakDays += 1;
            if (profile.CurrentStreakDays > previousLongest)
            {
                profile.LongestStreakDays = profile.CurrentStreakDays;
                isNewRecord = true;
            }

            streakChanged = "CONTINUED";
            profile.LastActivityDate = now;
            profile.UpdatedAt = now;
        }
        else
        {
            // Case d: Difference of 2 or more days (skipped day(s))
            if (profile.StreakFreezesAvailable > 0)
            {
                profile.StreakFreezesAvailable -= 1;
                // Streak is preserved
                streakChanged = "FROZEN";
                isNewRecord = false;
            }
            else
            {
                // Streak broken, resets to 1 starting today
                profile.CurrentStreakDays = 1;
                profile.LongestStreakDays = Math.Max(profile.LongestStreakDays, 1);
                streakChanged = "BROKEN";
                isNewRecord = false;
            }

            profile.LastActivityDate = now;
            profile.UpdatedAt = now;
        }

        // 5. Save in a single transaction
        await _dbContext.SaveChangesAsync(ct);

        // 6. Return StreakResult
        return new StreakResult
        {
            CurrentStreakDays = profile.CurrentStreakDays,
            LongestStreakDays = profile.LongestStreakDays,
            StreakFreezesAvailable = profile.StreakFreezesAvailable,
            StreakChanged = streakChanged,
            IsNewRecord = isNewRecord
        };
    }
}
