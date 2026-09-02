using FluentAssertions;
using HumanOS.Data;
using HumanOS.Models.Gamification;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HumanOS.Tests.Services;

public sealed class StreakServiceTests
{
    private static DbContextOptions<HumanOsDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task RegisterActivityAsync_FirstActivityEver_StartsStreakAtOne()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var fixedTime = new DateTime(2026, 9, 1, 10, 30, 0, DateTimeKind.Utc);

        await using var dbContext = new HumanOsDbContext(options);
        var service = new StreakService(dbContext, () => fixedTime);

        // Act
        var result = await service.RegisterActivityAsync(personId);

        // Assert
        result.Should().NotBeNull();
        result.CurrentStreakDays.Should().Be(1);
        result.LongestStreakDays.Should().Be(1);
        result.StreakFreezesAvailable.Should().Be(1);
        result.StreakChanged.Should().Be("STARTED");
        result.IsNewRecord.Should().BeTrue();

        var profile = await dbContext.PlayerProfiles.FirstOrDefaultAsync(x => x.PersonId == personId);
        profile.Should().NotBeNull();
        profile!.CurrentStreakDays.Should().Be(1);
        profile.LongestStreakDays.Should().Be(1);
        profile.LastActivityDate.Should().Be(fixedTime);
    }

    [Fact]
    public async Task RegisterActivityAsync_SameDayActivity_DoesNotChangeStreak()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var dayOneMorning = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var dayOneNight = new DateTime(2026, 9, 1, 22, 45, 0, DateTimeKind.Utc);

        await using var dbContext = new HumanOsDbContext(options);
        var service1 = new StreakService(dbContext, () => dayOneMorning);
        await service1.RegisterActivityAsync(personId);

        var service2 = new StreakService(dbContext, () => dayOneNight);

        // Act
        var result = await service2.RegisterActivityAsync(personId);

        // Assert
        result.CurrentStreakDays.Should().Be(1);
        result.LongestStreakDays.Should().Be(1);
        result.StreakChanged.Should().Be("ALREADY_COUNTED_TODAY");
        result.IsNewRecord.Should().BeFalse();

        var profile = await dbContext.PlayerProfiles.FirstOrDefaultAsync(x => x.PersonId == personId);
        profile.Should().NotBeNull();
        profile!.CurrentStreakDays.Should().Be(1);
    }

    [Fact]
    public async Task RegisterActivityAsync_NextDayActivity_ContinuesStreakAndIncrements()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var day1 = new DateTime(2026, 9, 1, 23, 50, 0, DateTimeKind.Utc);
        var day2 = new DateTime(2026, 9, 2, 0, 10, 0, DateTimeKind.Utc);

        await using var dbContext = new HumanOsDbContext(options);
        var service1 = new StreakService(dbContext, () => day1);
        await service1.RegisterActivityAsync(personId);

        var service2 = new StreakService(dbContext, () => day2);

        // Act
        var result = await service2.RegisterActivityAsync(personId);

        // Assert
        result.CurrentStreakDays.Should().Be(2);
        result.LongestStreakDays.Should().Be(2);
        result.StreakChanged.Should().Be("CONTINUED");
        result.IsNewRecord.Should().BeTrue();

        var profile = await dbContext.PlayerProfiles.FirstOrDefaultAsync(x => x.PersonId == personId);
        profile.Should().NotBeNull();
        profile!.CurrentStreakDays.Should().Be(2);
        profile.LongestStreakDays.Should().Be(2);
        profile.LastActivityDate.Should().Be(day2);
    }

    [Fact]
    public async Task RegisterActivityAsync_TwoDayGap_WithFreezeAvailable_PreservesStreakAndConsumesFreeze()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var day1 = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var day3 = new DateTime(2026, 9, 3, 14, 0, 0, DateTimeKind.Utc); // Skipped 2026-09-02

        await using var dbContext = new HumanOsDbContext(options);
        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            CurrentStreakDays = 5,
            LongestStreakDays = 5,
            StreakFreezesAvailable = 1,
            LastActivityDate = day1,
            UpdatedAt = day1
        });
        await dbContext.SaveChangesAsync();

        var service = new StreakService(dbContext, () => day3);

        // Act
        var result = await service.RegisterActivityAsync(personId);

        // Assert
        result.CurrentStreakDays.Should().Be(5); // Preserved
        result.LongestStreakDays.Should().Be(5);
        result.StreakFreezesAvailable.Should().Be(0); // Freeze consumed
        result.StreakChanged.Should().Be("FROZEN");
        result.IsNewRecord.Should().BeFalse();

        var profile = await dbContext.PlayerProfiles.FirstOrDefaultAsync(x => x.PersonId == personId);
        profile.Should().NotBeNull();
        profile!.CurrentStreakDays.Should().Be(5);
        profile.StreakFreezesAvailable.Should().Be(0);
        profile.LastActivityDate.Should().Be(day3);
    }

    [Fact]
    public async Task RegisterActivityAsync_TwoDayGap_WithoutFreeze_BreaksStreak()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var day1 = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var day3 = new DateTime(2026, 9, 3, 14, 0, 0, DateTimeKind.Utc); // Skipped 2026-09-02

        await using var dbContext = new HumanOsDbContext(options);
        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            CurrentStreakDays = 12,
            LongestStreakDays = 12,
            StreakFreezesAvailable = 0, // No freeze left
            LastActivityDate = day1,
            UpdatedAt = day1
        });
        await dbContext.SaveChangesAsync();

        var service = new StreakService(dbContext, () => day3);

        // Act
        var result = await service.RegisterActivityAsync(personId);

        // Assert
        result.CurrentStreakDays.Should().Be(1); // Reset
        result.LongestStreakDays.Should().Be(12); // Highest preserved
        result.StreakFreezesAvailable.Should().Be(0);
        result.StreakChanged.Should().Be("BROKEN");
        result.IsNewRecord.Should().BeFalse();

        var profile = await dbContext.PlayerProfiles.FirstOrDefaultAsync(x => x.PersonId == personId);
        profile.Should().NotBeNull();
        profile!.CurrentStreakDays.Should().Be(1);
        profile.LongestStreakDays.Should().Be(12);
        profile.LastActivityDate.Should().Be(day3);
    }

    [Fact]
    public async Task RegisterActivityAsync_LongestStreakDays_UpdatesCorrectlyWhenBeatingRecord()
    {
        // Arrange:
        // Person previous record was 4. Current streak is 4.
        // Today is consecutive day -> Current becomes 5 -> Longest becomes 5 -> IsNewRecord = true.
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var dayYesterday = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var dayToday = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);

        await using var dbContext = new HumanOsDbContext(options);
        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            CurrentStreakDays = 4,
            LongestStreakDays = 4,
            StreakFreezesAvailable = 1,
            LastActivityDate = dayYesterday,
            UpdatedAt = dayYesterday
        });
        await dbContext.SaveChangesAsync();

        var service = new StreakService(dbContext, () => dayToday);

        // Act
        var result = await service.RegisterActivityAsync(personId);

        // Assert
        result.CurrentStreakDays.Should().Be(5);
        result.LongestStreakDays.Should().Be(5);
        result.StreakChanged.Should().Be("CONTINUED");
        result.IsNewRecord.Should().BeTrue();

        var profile = await dbContext.PlayerProfiles.FirstOrDefaultAsync(x => x.PersonId == personId);
        profile.Should().NotBeNull();
        profile!.CurrentStreakDays.Should().Be(5);
        profile.LongestStreakDays.Should().Be(5);
    }

    [Fact]
    public async Task RegisterActivityAsync_LongestStreakDays_DoesNotMarkNewRecordIfBelowAllTimeHigh()
    {
        // Arrange:
        // Historic record was 20. But current streak is 3.
        // Today is consecutive day -> Current becomes 4 -> Longest remains 20 -> IsNewRecord = false.
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var dayYesterday = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var dayToday = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);

        await using var dbContext = new HumanOsDbContext(options);
        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            CurrentStreakDays = 3,
            LongestStreakDays = 20,
            StreakFreezesAvailable = 1,
            LastActivityDate = dayYesterday,
            UpdatedAt = dayYesterday
        });
        await dbContext.SaveChangesAsync();

        var service = new StreakService(dbContext, () => dayToday);

        // Act
        var result = await service.RegisterActivityAsync(personId);

        // Assert
        result.CurrentStreakDays.Should().Be(4);
        result.LongestStreakDays.Should().Be(20);
        result.StreakChanged.Should().Be("CONTINUED");
        result.IsNewRecord.Should().BeFalse();
    }
}
