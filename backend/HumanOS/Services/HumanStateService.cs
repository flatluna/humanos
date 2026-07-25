using HumanOS.Contracts.HumanState;
using HumanOS.Data;
using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// "Estado Humano" daily check-in — powers the /today dashboard's
/// self-reported focus/energy/purpose/confidence metrics (Apple-Health
/// style). One <see cref="HumanState"/> row per person per calendar day
/// (UTC date of <see cref="HumanState.RecordedAt"/>).
/// </summary>
public sealed class HumanStateService
{
    private readonly HumanOsDbContext _dbContext;

    public HumanStateService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HumanStateCheckInResponse> GetTodayAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        var todayEntry = await _dbContext.HumanStates
            .AsNoTracking()
            .Where(x => x.PersonId == personId && x.RecordedAt >= today && x.RecordedAt < today.AddDays(1))
            .OrderByDescending(x => x.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var streak = await ComputeStreakAsync(personId, today, cancellationToken);

        return new HumanStateCheckInResponse
        {
            PersonId = personId,
            RecordedAt = todayEntry?.RecordedAt,
            Energy = todayEntry?.Energy,
            Focus = todayEntry?.Focus,
            Purpose = todayEntry?.Purpose,
            Confidence = todayEntry?.Confidence,
            Streak = streak,
            HasCheckedInToday = todayEntry is not null
        };
    }

    public async Task<HumanStateCheckInResponse> UpsertTodayAsync(
        Guid personId,
        int energy,
        int focus,
        int purpose,
        int confidence,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        var existing = await _dbContext.HumanStates
            .Where(x => x.PersonId == personId && x.RecordedAt >= today && x.RecordedAt < today.AddDays(1))
            .OrderByDescending(x => x.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            var person = await _dbContext.People
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PersonId == personId, cancellationToken);

            existing = new HumanState
            {
                HumanStateId = Guid.NewGuid(),
                PersonId = personId,
                TenantId = person?.TenantId,
                RecordedAt = DateTime.UtcNow
            };
            _dbContext.HumanStates.Add(existing);
        }

        existing.Energy = Clamp(energy);
        existing.Focus = Clamp(focus);
        existing.Purpose = Clamp(purpose);
        existing.Confidence = Clamp(confidence);

        var streak = await ComputeStreakAsync(personId, today, cancellationToken, includingToday: true);
        existing.Streak = streak;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new HumanStateCheckInResponse
        {
            PersonId = personId,
            RecordedAt = existing.RecordedAt,
            Energy = existing.Energy,
            Focus = existing.Focus,
            Purpose = existing.Purpose,
            Confidence = existing.Confidence,
            Streak = streak,
            HasCheckedInToday = true
        };
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);

    /// <summary>Counts consecutive calendar days (walking backward from
    /// <paramref name="today"/>) that have a check-in row. When
    /// <paramref name="includingToday"/> is true, today is assumed to
    /// already have one (used right after an upsert, before the caller's
    /// own row would otherwise be counted twice via a redundant query).</summary>
    private async Task<int> ComputeStreakAsync(
        Guid personId,
        DateTime today,
        CancellationToken cancellationToken,
        bool includingToday = false)
    {
        var recordedDates = await _dbContext.HumanStates
            .AsNoTracking()
            .Where(x => x.PersonId == personId && x.RecordedAt <= today.AddDays(1))
            .Select(x => x.RecordedAt.Date)
            .Distinct()
            .ToListAsync(cancellationToken);

        var dateSet = recordedDates.ToHashSet();

        var streak = 0;
        var cursor = today;
        if (!includingToday && !dateSet.Contains(today))
        {
            return 0;
        }

        while (dateSet.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }
}
