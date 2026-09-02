using FluentAssertions;
using HumanOS.Data;
using HumanOS.Models.Gamification;
using HumanOS.Models.Learning;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HumanOS.Tests.Services;

public sealed class RewardEngineServiceTests
{
    private static DbContextOptions<HumanOsDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task GrantEngramRewardAsync_CalculatesExpectedPoints_WithKnownMultipliersAndSurprise()
    {
        // Arrange:
        // Base points = 80 (from EngramImpactScore)
        // Difficulty level = 4 -> 1.35
        // Memory recall pct = 40% (25-49 range) -> 1.8
        // Surprise roll = 95 (93-98 range) -> DOUBLE (2.0x)
        // PointsAwarded = round(80 * 1.35 * 1.8 * 2.0) = round(388.8) = 389
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var engramId = "engram-neuro-1";

        await using var dbContext = new HumanOsDbContext(options);
        dbContext.EngramImpactScores.Add(new EngramImpactScore
        {
            Id = Guid.NewGuid(),
            EmployeeId = personId,
            CourseId = courseId,
            EngramId = engramId,
            Score = 80,
            Tier = "Critical",
            PrimarySignal = "KPI",
            Reason = "Direct impact",
            GeneratedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = new RewardEngineService(dbContext, randomRollProvider: () => 95);

        // Act
        var result = await service.GrantEngramRewardAsync(
            personId: personId,
            courseId: courseId,
            engramId: engramId,
            sourceType: "Structured",
            engramDifficultyLevel: 4,
            memoryRecallPct: 40.0);

        // Assert
        result.Should().NotBeNull();
        result.AlreadyEarned.Should().BeFalse();
        result.PointsBase.Should().Be(80);
        result.DifficultyMultiplier.Should().Be(1.35m);
        result.MemoryEffortMultiplier.Should().Be(1.8m);
        result.SurpriseMultiplier.Should().Be(2.0m);
        result.PointsAwarded.Should().Be(389);
        result.WasSurprise.Should().BeTrue();
        result.SurpriseType.Should().Be("DOUBLE");
        result.NewTotalPoints.Should().Be(389);
        result.NewLevel.Should().Be(1);
        result.NewLevelName.Should().Be("Neurona");
        result.LeveledUp.Should().BeFalse();

        // Verify database state
        var ledgerEntries = await dbContext.RewardLedgerEntries.ToListAsync();
        ledgerEntries.Should().HaveCount(1);
        var entry = ledgerEntries[0];
        entry.PersonId.Should().Be(personId);
        entry.CourseId.Should().Be(courseId);
        entry.EngramId.Should().Be(engramId);
        entry.PointsAwarded.Should().Be(389);
        entry.WasSurprise.Should().BeTrue();
        entry.SurpriseType.Should().Be("DOUBLE");
        entry.SourceType.Should().Be("Structured");

        var profile = await dbContext.PlayerProfiles.FirstOrDefaultAsync(x => x.PersonId == personId);
        profile.Should().NotBeNull();
        profile!.TotalPoints.Should().Be(389);
        profile.TotalEngramsMastered.Should().Be(1);
        profile.CurrentLevel.Should().Be(1);
        profile.CurrentLevelName.Should().Be("Neurona");
    }

    [Fact]
    public async Task GrantEngramRewardAsync_IsIdempotent_SecondCallDoesNotDuplicatePointsOrEntries()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var engramId = "engram-idempotent-test";

        await using var dbContext = new HumanOsDbContext(options);
        var service = new RewardEngineService(dbContext, randomRollProvider: () => 50); // No surprise (1.0x)

        // Act 1: First call -> earns 50 points (neutral fallback)
        var firstResult = await service.GrantEngramRewardAsync(
            personId: personId,
            courseId: courseId,
            engramId: engramId,
            sourceType: "Graph",
            engramDifficultyLevel: null,
            memoryRecallPct: null);

        firstResult.AlreadyEarned.Should().BeFalse();
        firstResult.PointsAwarded.Should().Be(50);
        firstResult.NewTotalPoints.Should().Be(50);

        // Act 2: Second call for same person + course + engram
        var secondResult = await service.GrantEngramRewardAsync(
            personId: personId,
            courseId: courseId,
            engramId: engramId,
            sourceType: "Graph",
            engramDifficultyLevel: 5,
            memoryRecallPct: 10.0);

        // Assert
        secondResult.AlreadyEarned.Should().BeTrue();
        secondResult.PointsAwarded.Should().Be(50);
        secondResult.NewTotalPoints.Should().Be(50);
        secondResult.LeveledUp.Should().BeFalse();

        // Verify only 1 entry in ledger and profile points were NOT added twice
        var ledgerEntries = await dbContext.RewardLedgerEntries.ToListAsync();
        ledgerEntries.Should().HaveCount(1);

        var profile = await dbContext.PlayerProfiles.FirstOrDefaultAsync(x => x.PersonId == personId);
        profile.Should().NotBeNull();
        profile!.TotalPoints.Should().Be(50);
        profile.TotalEngramsMastered.Should().Be(1);
    }

    [Fact]
    public async Task GrantEngramRewardAsync_CorrectlyLevelsUp_WhenCrossingThreshold()
    {
        // Arrange:
        // Existing profile has 450 points (Level 1 Neurona)
        // Awarding 100 points -> 550 points -> Level 2 Sinapsis -> LeveledUp = true
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var engramId = "engram-level-crossing";

        await using var dbContext = new HumanOsDbContext(options);
        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalPoints = 450,
            CurrentLevel = 1,
            CurrentLevelName = "Neurona",
            TotalEngramsMastered = 5,
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        });
        dbContext.EngramImpactScores.Add(new EngramImpactScore
        {
            Id = Guid.NewGuid(),
            EmployeeId = personId,
            CourseId = courseId,
            EngramId = engramId,
            Score = 100,
            Tier = "Critical",
            GeneratedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = new RewardEngineService(dbContext, randomRollProvider: () => 10); // No surprise (1.0x)

        // Act
        var result = await service.GrantEngramRewardAsync(
            personId: personId,
            courseId: courseId,
            engramId: engramId,
            sourceType: "Structured",
            engramDifficultyLevel: 1, // 1.0x
            memoryRecallPct: 90.0);   // >= 80 -> 1.0x

        // Assert
        result.PointsAwarded.Should().Be(100);
        result.NewTotalPoints.Should().Be(550);
        result.NewLevel.Should().Be(2);
        result.NewLevelName.Should().Be("Sinapsis");
        result.LeveledUp.Should().BeTrue();

        var profile = await dbContext.PlayerProfiles.FirstOrDefaultAsync(x => x.PersonId == personId);
        profile.Should().NotBeNull();
        profile!.TotalPoints.Should().Be(550);
        profile.CurrentLevel.Should().Be(2);
        profile.CurrentLevelName.Should().Be("Sinapsis");
        profile.TotalEngramsMastered.Should().Be(6);
    }

    [Theory]
    [InlineData(0, 1, "Neurona")]
    [InlineData(499, 1, "Neurona")]
    [InlineData(500, 2, "Sinapsis")]
    [InlineData(1499, 2, "Sinapsis")]
    [InlineData(1500, 3, "Circuito")]
    [InlineData(3499, 3, "Circuito")]
    [InlineData(3500, 4, "Red Neuronal")]
    [InlineData(6999, 4, "Red Neuronal")]
    [InlineData(7000, 5, "Mente Maestra")]
    [InlineData(12000, 5, "Mente Maestra")]
    public void CalculateLevel_MapsBandsAccurately(int points, int expectedLevel, string expectedName)
    {
        var (level, name) = RewardEngineService.CalculateLevel(points);
        level.Should().Be(expectedLevel);
        name.Should().Be(expectedName);
    }

    [Theory]
    [InlineData(100, 1.0)]
    [InlineData(80, 1.0)]
    [InlineData(79.9, 1.3)]
    [InlineData(50, 1.3)]
    [InlineData(49.9, 1.8)]
    [InlineData(25, 1.8)]
    [InlineData(24.9, 2.5)]
    [InlineData(0, 2.5)]
    public async Task GrantEngramRewardAsync_AppliesMemoryEffortBands_Correctly(double memoryRecallPct, double expectedMultiplier)
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var engramId = $"engram-recall-{memoryRecallPct}";

        await using var dbContext = new HumanOsDbContext(options);
        var service = new RewardEngineService(dbContext, randomRollProvider: () => 0); // No surprise (1.0x)

        var result = await service.GrantEngramRewardAsync(
            personId: personId,
            courseId: courseId,
            engramId: engramId,
            sourceType: "Structured",
            engramDifficultyLevel: 1, // 1.0x
            memoryRecallPct: memoryRecallPct);

        result.MemoryEffortMultiplier.Should().Be((decimal)expectedMultiplier);
    }

    [Theory]
    [InlineData(50, 1.0, "NONE", false)]
    [InlineData(74, 1.0, "NONE", false)]
    [InlineData(75, 1.5, "BONUS", true)]
    [InlineData(92, 1.5, "BONUS", true)]
    [InlineData(93, 2.0, "DOUBLE", true)]
    [InlineData(98, 2.0, "DOUBLE", true)]
    [InlineData(99, 3.0, "JACKPOT", true)]
    [InlineData(100, 3.0, "JACKPOT", true)]
    public async Task GrantEngramRewardAsync_AppliesSurpriseBands_Correctly(int roll, double expectedMultiplier, string expectedType, bool expectedWasSurprise)
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var engramId = $"engram-surprise-{roll}";

        await using var dbContext = new HumanOsDbContext(options);
        var service = new RewardEngineService(dbContext, randomRollProvider: () => roll);

        var result = await service.GrantEngramRewardAsync(
            personId: personId,
            courseId: courseId,
            engramId: engramId,
            sourceType: "Graph",
            engramDifficultyLevel: 1,
            memoryRecallPct: null);

        result.SurpriseMultiplier.Should().Be((decimal)expectedMultiplier);
        result.SurpriseType.Should().Be(expectedType);
        result.WasSurprise.Should().Be(expectedWasSurprise);
    }
}
