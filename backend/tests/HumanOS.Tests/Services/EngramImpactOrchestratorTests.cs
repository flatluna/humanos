using FluentAssertions;
using HumanOS.Agents;
using HumanOS.Contracts.EmployeeScoring;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Models.Learning;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HumanOS.Tests.Services;

public sealed class EngramImpactOrchestratorTests
{
    private static DbContextOptions<HumanOsDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task GetOrGenerateScoresAsync_CacheHit_ReturnsExistingScoresWithoutCallingAgent()
    {
        var options = CreateInMemoryOptions();
        var employeeId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);

        // Seed existing cached scores
        dbContext.EngramImpactScores.Add(new EngramImpactScore
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            CourseId = courseId,
            EngramId = "cached-engram-1",
            Score = 88,
            Tier = "Critical",
            PrimarySignal = "KPI",
            Reason = "Cached reason.",
            GeneratedAt = DateTime.UtcNow.AddHours(-2)
        });
        await dbContext.SaveChangesAsync();

        var profileService = new EmployeeScoringProfileService(dbContext);
        var unconfiguredAgent = new EngramImpactScoringAgent(new ConfigurationBuilder().Build()); // Not configured
        var persistenceService = new EngramImpactScorePersistenceService(dbContext);

        var orchestrator = new EngramImpactOrchestrator(
            dbContext,
            profileService,
            unconfiguredAgent,
            persistenceService);

        // When forceRefresh is false, it should hit cache and NOT invoke the unconfigured agent (which would throw if invoked)
        var result = await orchestrator.GetOrGenerateScoresAsync(employeeId, courseId, forceRefresh: false);

        result.Should().HaveCount(1);
        result[0].EngramId.Should().Be("cached-engram-1");
        result[0].Score.Should().Be(88);
        result[0].Tier.Should().Be("Critical");
    }

    [Fact]
    public async Task GetOrGenerateScoresAsync_ThrowsClearException_WhenCourseHasNoEngrams()
    {
        var options = CreateInMemoryOptions();
        var employeeId = Guid.NewGuid();
        var emptyCourseId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        var profileService = new EmployeeScoringProfileService(dbContext);
        var agent = new EngramImpactScoringAgent(new ConfigurationBuilder().Build());
        var persistenceService = new EngramImpactScorePersistenceService(dbContext);

        var orchestrator = new EngramImpactOrchestrator(
            dbContext,
            profileService,
            agent,
            persistenceService);

        var act = () => orchestrator.GetOrGenerateScoresAsync(employeeId, emptyCourseId, forceRefresh: true);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Course Engrams Retrieval stage*");
    }
}
