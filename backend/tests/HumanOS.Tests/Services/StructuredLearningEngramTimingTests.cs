using FluentAssertions;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HumanOS.Tests.Services;

public sealed class StructuredLearningEngramTimingTests
{
    private static HumanOsDbContext CreateDb() => new(new DbContextOptionsBuilder<HumanOsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static StructuredLearningEngramStatsService Stats(HumanOsDbContext db) =>
        new(db, new ConfigurationBuilder().Build());

    [Theory]
    [InlineData("Hypothesis")]
    [InlineData("Teaching")]
    [InlineData("DeepDive")]
    [InlineData("Production")]
    [InlineData("Recall")]
    [InlineData("Assessment")]
    public async Task VisibleIntervals_SumReturnsWithoutAddingVisitsOrHiddenTime(string stepType)
    {
        await using var db = CreateDb();
        var service = new StructuredLearningEngramSessionService(db);
        var section = Guid.NewGuid();
        var session = await service.StartSessionAsync(null, null, null, default);
        var first = Guid.NewGuid();
        await service.RecordStepActivityAsync(session.StructuredLearningEngramSessionId, section, stepType, first, 0, false, default);
        await service.RecordStepActivityAsync(session.StructuredLearningEngramSessionId, section, stepType, first, 5_500, true, default);
        await service.RecordStepActivityAsync(session.StructuredLearningEngramSessionId, section, stepType, first, 5_500, true, default);
        await service.RecordStepActivityAsync(session.StructuredLearningEngramSessionId, section, stepType, first, 2_000, false, default);
        await service.RecordStepActivityAsync(session.StructuredLearningEngramSessionId, section, stepType, Guid.NewGuid(), 3_500, true, default);

        var result = await Stats(db).GetEngramStatsAsync(section, default);
        var step = result.StepSummaries.Single(s => s.StepType == stepType);
        step.TotalDurationSeconds.Should().Be(9);
        step.AverageDurationSeconds.Should().Be(9);
        step.SessionsCount.Should().Be(1);
        step.HasEstimatedTiming.Should().BeFalse();
        result.TotalTimeSeconds.Should().Be(9);
        result.TotalSessionsVisited.Should().Be(1);
    }

    [Fact]
    public async Task HistoricalUnclosedHypothesis_EndsAtTeachingNotCourseEnd()
    {
        await using var db = CreateDb();
        var section = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(-1);
        var session = new StructuredLearningEngramSession { StartedDate = start, LastActivityDate = start.AddMinutes(20) };
        db.StructuredLearningEngramSessions.Add(session);
        db.StructuredLearningEngramSessionSteps.AddRange(
            Legacy(session, section, "Hypothesis", start),
            Legacy(session, section, "Teaching", start.AddSeconds(5)));
        await db.SaveChangesAsync();

        var result = await Stats(db).GetEngramStatsAsync(section, default);
        result.StepSummaries.Single(s => s.StepType == "Hypothesis").TotalDurationSeconds.Should().Be(5);
        result.StepSummaries.Single(s => s.StepType == "Hypothesis").HasEstimatedTiming.Should().BeTrue();
        result.TotalTimeSeconds.Should().Be(1200);
    }

    [Fact]
    public async Task HistoricalLateCompletion_IsBoundedByNextEngramInSameCourse()
    {
        await using var db = CreateDb();
        var section = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(-1);
        var session = new StructuredLearningEngramSession { StartedDate = start, LastActivityDate = start.AddMinutes(20) };
        var first = Legacy(session, section, "Teaching", start);
        first.CompletedDate = start.AddMinutes(15);
        db.StructuredLearningEngramSessions.Add(session);
        db.StructuredLearningEngramSessionSteps.AddRange(first, Legacy(session, Guid.NewGuid(), "Hypothesis", start.AddSeconds(7)));
        await db.SaveChangesAsync();

        var result = await Stats(db).GetEngramStatsAsync(section, default);
        result.TotalTimeSeconds.Should().Be(7);
        (await Stats(db).GetSessionStatsAsync(session.StructuredLearningEngramSessionId, default))!
            .Steps.Single(s => s.StructuredLearningDocumentSectionId == section).DurationSeconds.Should().Be(7);
        first.CompletedDate.Should().Be(start.AddMinutes(15));
    }

    [Fact]
    public async Task ActiveHeartbeat_DoesNotGrowWithWallTime_AndCanExceedThirtyMinutes()
    {
        await using var db = CreateDb();
        var service = new StructuredLearningEngramSessionService(db);
        var section = Guid.NewGuid();
        var session = await service.StartSessionAsync(null, null, null, default);
        await service.RecordStepActivityAsync(session.StructuredLearningEngramSessionId, section, "Teaching",
            Guid.NewGuid(), 2_100_000, false, default);
        var result = await Stats(db).GetEngramStatsAsync(section, default);
        result.TotalTimeSeconds.Should().Be(2100);
        await service.CloseSessionAsync(session.StructuredLearningEngramSessionId, default);
        session.DurationSeconds.Should().Be(2100);
        (await Stats(db).GetSessionStatsAsync(session.StructuredLearningEngramSessionId, default))!
            .DurationSeconds.Should().Be(2100);
    }

    [Fact]
    public async Task LateExitAfterCourseClose_UpdatesDurationWithoutReopeningSession()
    {
        await using var db = CreateDb();
        var service = new StructuredLearningEngramSessionService(db);
        var session = await service.StartSessionAsync(null, null, null, default);
        var section = Guid.NewGuid();
        var interval = Guid.NewGuid();
        await service.RecordStepActivityAsync(session.StructuredLearningEngramSessionId, section, "Recall", interval, 10_000, false, default);
        await service.CloseSessionAsync(session.StructuredLearningEngramSessionId, default);
        var closed = session.CompletedDate;
        await service.RecordStepActivityAsync(session.StructuredLearningEngramSessionId, section, "Recall", interval, 12_000, true, default);
        session.CompletedDate.Should().Be(closed);
        session.DurationSeconds.Should().Be(12);
    }

    [Fact]
    public async Task LegacyCompletion_IsIdempotent_AndNewStepClosesPrevious()
    {
        await using var db = CreateDb();
        var service = new StructuredLearningEngramSessionService(db);
        var session = await service.StartSessionAsync(null, null, null, default);
        var section = Guid.NewGuid();
        await service.RecordStepStartAsync(session.StructuredLearningEngramSessionId, section, "Hypothesis", default);
        await service.RecordStepStartAsync(session.StructuredLearningEngramSessionId, section, "Teaching", default);
        var hypothesis = await db.StructuredLearningEngramSessionSteps.SingleAsync(s => s.StepType == "Hypothesis");
        var completion = hypothesis.CompletedDate;
        completion.Should().NotBeNull();
        await service.RecordStepCompleteAsync(session.StructuredLearningEngramSessionId, section, "Hypothesis", default);
        hypothesis.CompletedDate.Should().Be(completion);
    }

    [Fact]
    public async Task IntervalCannotBeReusedForAnotherStep()
    {
        await using var db = CreateDb();
        var service = new StructuredLearningEngramSessionService(db);
        var session = await service.StartSessionAsync(null, null, null, default);
        var section = Guid.NewGuid();
        var interval = Guid.NewGuid();
        await service.RecordStepActivityAsync(session.StructuredLearningEngramSessionId, section, "Recall", interval, 5000, false, default);
        var act = () => service.RecordStepActivityAsync(session.StructuredLearningEngramSessionId, section, "Assessment", interval, 6000, true, default);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AverageUsesDistinctCourseVisits_AndAllSixStepsShareTheSameSessionTotal()
    {
        await using var db = CreateDb();
        var service = new StructuredLearningEngramSessionService(db);
        var section = Guid.NewGuid();
        var first = await service.StartSessionAsync(null, null, null, default);
        var types = new[] { "Hypothesis", "Teaching", "DeepDive", "Production", "Recall", "Assessment" };
        foreach (var type in types)
        {
            await service.RecordStepActivityAsync(first.StructuredLearningEngramSessionId, section, type,
                Guid.NewGuid(), 5000, true, default);
        }
        await service.RecordStepActivityAsync(first.StructuredLearningEngramSessionId, section, "Hypothesis",
            Guid.NewGuid(), 3000, true, default);
        var second = await service.StartSessionAsync(null, null, null, default);
        await service.RecordStepActivityAsync(second.StructuredLearningEngramSessionId, section, "Hypothesis",
            Guid.NewGuid(), 4000, true, default);
        var result = await Stats(db).GetEngramStatsAsync(section, default);
        var hypothesis = result.StepSummaries.Single(s => s.StepType == "Hypothesis");
        hypothesis.TotalDurationSeconds.Should().Be(12);
        hypothesis.SessionsCount.Should().Be(2);
        hypothesis.AverageDurationSeconds.Should().Be(6);
        result.TotalTimeSeconds.Should().Be(37);
        (await Stats(db).GetSessionStatsAsync(first.StructuredLearningEngramSessionId, default))!
            .DurationSeconds.Should().Be(33);
    }

    [Fact]
    public async Task HistoricalKnownLongStep_IsNotArbitrarilyCapped()
    {
        await using var db = CreateDb();
        var start = DateTime.UtcNow.AddHours(-2);
        var section = Guid.NewGuid();
        var session = new StructuredLearningEngramSession { StartedDate = start, LastActivityDate = start.AddHours(1) };
        var step = Legacy(session, section, "Teaching", start);
        step.CompletedDate = start.AddMinutes(40);
        db.StructuredLearningEngramSessions.Add(session);
        db.StructuredLearningEngramSessionSteps.Add(step);
        await db.SaveChangesAsync();
        (await Stats(db).GetEngramStatsAsync(section, default)).TotalTimeSeconds.Should().Be(2400);
    }

    private static StructuredLearningEngramSessionStep Legacy(StructuredLearningEngramSession session, Guid section, string type, DateTime start) =>
        new() { StructuredLearningEngramSessionId = session.StructuredLearningEngramSessionId,
            StructuredLearningDocumentSectionId = section, StepType = type, StartedDate = start };
}
