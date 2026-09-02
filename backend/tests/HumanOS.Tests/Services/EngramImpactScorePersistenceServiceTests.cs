using FluentAssertions;
using HumanOS.Contracts.EmployeeScoring;
using HumanOS.Data;
using HumanOS.Models.Learning;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HumanOS.Tests.Services;

public sealed class EngramImpactScorePersistenceServiceTests
{
    private static DbContextOptions<HumanOsDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task SaveScoresAsync_InsertsNewScores_WhenNoPriorScoresExist()
    {
        var options = CreateInMemoryOptions();
        var employeeId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        var service = new EngramImpactScorePersistenceService(dbContext);

        var scores = new List<EngramScoreResult>
        {
            new()
            {
                EngramId = "engram-1",
                Score = 95,
                Tier = "Critical",
                PrimarySignal = "KPI",
                Reason = "Supports key performance indicator directly."
            },
            new()
            {
                EngramId = "engram-2",
                Score = 40,
                Tier = "Optional",
                PrimarySignal = "Role",
                Reason = "Foundational background knowledge."
            }
        };

        await service.SaveScoresAsync(employeeId, courseId, scores);

        var persisted = await dbContext.EngramImpactScores
            .Where(x => x.EmployeeId == employeeId && x.CourseId == courseId)
            .ToListAsync();

        persisted.Should().HaveCount(2);
        persisted.Should().ContainSingle(x => x.EngramId == "engram-1" && x.Score == 95 && x.Tier == "Critical" && x.PrimarySignal == "KPI");
        persisted.Should().ContainSingle(x => x.EngramId == "engram-2" && x.Score == 40 && x.Tier == "Optional" && x.PrimarySignal == "Role");
    }

    [Fact]
    public async Task SaveScoresAsync_UpsertsExistingScores_WithoutCreatingDuplicates()
    {
        var options = CreateInMemoryOptions();
        var employeeId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        var initialScore = new EngramImpactScore
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            CourseId = courseId,
            EngramId = "engram-1",
            Score = 50,
            Tier = "Important",
            PrimarySignal = "Role",
            Reason = "Initial baseline score.",
            GeneratedAt = DateTime.UtcNow.AddDays(-5)
        };
        dbContext.EngramImpactScores.Add(initialScore);
        await dbContext.SaveChangesAsync();

        var service = new EngramImpactScorePersistenceService(dbContext);

        var updatedScores = new List<EngramScoreResult>
        {
            new()
            {
                EngramId = "engram-1", // Existing - should update
                Score = 90,
                Tier = "Critical",
                PrimarySignal = "Coaching",
                Reason = "Updated based on latest coaching note."
            },
            new()
            {
                EngramId = "engram-new", // New - should insert
                Score = 75,
                Tier = "Important",
                PrimarySignal = "Skill",
                Reason = "Newly added course engram."
            }
        };

        await service.SaveScoresAsync(employeeId, courseId, updatedScores);

        var allRows = await dbContext.EngramImpactScores
            .Where(x => x.EmployeeId == employeeId && x.CourseId == courseId)
            .ToListAsync();

        allRows.Should().HaveCount(2);

        var updated = allRows.Single(x => x.EngramId == "engram-1");
        updated.Id.Should().Be(initialScore.Id); // Preserves primary key
        updated.Score.Should().Be(90);
        updated.Tier.Should().Be("Critical");
        updated.PrimarySignal.Should().Be("Coaching");
        updated.Reason.Should().Be("Updated based on latest coaching note.");

        var inserted = allRows.Single(x => x.EngramId == "engram-new");
        inserted.Score.Should().Be(75);
        inserted.Tier.Should().Be("Important");
    }

    [Fact]
    public async Task SaveScoresAsync_DoesNothing_WhenScoresListIsEmpty()
    {
        var options = CreateInMemoryOptions();
        var employeeId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        var service = new EngramImpactScorePersistenceService(dbContext);

        await service.SaveScoresAsync(employeeId, courseId, []);

        var count = await dbContext.EngramImpactScores.CountAsync();
        count.Should().Be(0);
    }
}
