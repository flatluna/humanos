using FluentAssertions;
using HumanOS.Data;
using HumanOS.Models.Gamification;
using HumanOS.Models.Learning;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HumanOS.Tests.Services;

public sealed class GamificationOrchestratorTests
{
    private static DbContextOptions<HumanOsDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task ProcessEngramMasteryAsync_ExecutesAllThreeServicesInOrder_AndReturnsCompleteBundle()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var engramId = "engram-mastery-order-test";

        await using var dbContext = new HumanOsDbContext(options);
        await CatalogSeeder.SeedBadgesAsync(dbContext);

        dbContext.EngramImpactScores.Add(new EngramImpactScore
        {
            Id = Guid.NewGuid(),
            EmployeeId = personId,
            CourseId = courseId,
            EngramId = engramId,
            Score = 75,
            Tier = "Important",
            GeneratedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var rewardEngine = new RewardEngineService(dbContext, randomRollProvider: () => 0); // 1.0x surprise
        var streakService = new StreakService(dbContext);
        var badgeService = new BadgeService(dbContext);
        var orchestrator = new GamificationOrchestrator(
            rewardEngine, streakService, badgeService, NullLogger<GamificationOrchestrator>.Instance);

        // Act
        var bundle = await orchestrator.ProcessEngramMasteryAsync(
            personId: personId,
            courseId: courseId,
            engramId: engramId,
            sourceType: "Graph");

        // Assert
        bundle.Should().NotBeNull();
        bundle.AlreadyEarned.Should().BeFalse();

        // 1. Reward
        bundle.Reward.Should().NotBeNull();
        bundle.Reward!.PointsBase.Should().Be(75);
        bundle.Reward.PointsAwarded.Should().Be(75);
        bundle.Reward.NewTotalPoints.Should().Be(75);

        // 2. Streak
        bundle.Streak.Should().NotBeNull();
        bundle.Streak!.CurrentStreakDays.Should().Be(1);
        bundle.Streak.StreakChanged.Should().Be("STARTED");

        // 3. Badges (first_neuron should unlock because TotalEngramsMastered is now 1)
        bundle.NewBadges.Should().NotBeEmpty();
        bundle.NewBadges.Should().ContainSingle(b => b.BadgeCode == "first_neuron");

        // DB Assertions
        var profile = await dbContext.PlayerProfiles.FirstOrDefaultAsync(p => p.PersonId == personId);
        profile.Should().NotBeNull();
        profile!.TotalPoints.Should().Be(75);
        profile.CurrentStreakDays.Should().Be(1);
        profile.TotalEngramsMastered.Should().Be(1);

        var playerBadges = await dbContext.PlayerBadges.Where(p => p.PersonId == personId).ToListAsync();
        playerBadges.Should().ContainSingle(b => b.BadgeCode == "first_neuron");
    }

    [Fact]
    public async Task ProcessEngramMasteryAsync_WhenAlreadyEarned_PreservesIdempotencyWhileUpdatingActivity()
    {
        // Arrange
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var engramId = "engram-idempotency-orchestrator";

        await using var dbContext = new HumanOsDbContext(options);
        await CatalogSeeder.SeedBadgesAsync(dbContext);

        var rewardEngine = new RewardEngineService(dbContext, randomRollProvider: () => 0);
        var streakService = new StreakService(dbContext);
        var badgeService = new BadgeService(dbContext);
        var orchestrator = new GamificationOrchestrator(
            rewardEngine, streakService, badgeService, NullLogger<GamificationOrchestrator>.Instance);

        // First pass -> earns points & badge
        var firstBundle = await orchestrator.ProcessEngramMasteryAsync(personId, courseId, engramId, "Structured");
        firstBundle.AlreadyEarned.Should().BeFalse();
        firstBundle.Reward!.PointsAwarded.Should().Be(50); // Fallback points

        // Second pass for the exact same engram
        var secondBundle = await orchestrator.ProcessEngramMasteryAsync(personId, courseId, engramId, "Structured");

        // Assert
        secondBundle.AlreadyEarned.Should().BeTrue();
        secondBundle.Reward!.AlreadyEarned.Should().BeTrue();
        secondBundle.Reward.NewTotalPoints.Should().Be(50); // Not doubled!
        secondBundle.Streak!.StreakChanged.Should().Be("ALREADY_COUNTED_TODAY");
        secondBundle.NewBadges.Should().BeEmpty(); // No duplicate badges

        // Database checks
        var ledgerEntries = await dbContext.RewardLedgerEntries.Where(r => r.PersonId == personId).ToListAsync();
        ledgerEntries.Should().HaveCount(1);

        var profile = await dbContext.PlayerProfiles.FirstOrDefaultAsync(p => p.PersonId == personId);
        profile!.TotalPoints.Should().Be(50);
        profile.TotalEngramsMastered.Should().Be(1);
    }
}
