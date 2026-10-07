using System.Text;
using FluentAssertions;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HumanOS.Tests.Services;

public sealed class StructuredLearningTokenUsageTests
{
    private static HumanOsDbContext CreateDb(string? name = null) => new(new DbContextOptionsBuilder<HumanOsDbContext>()
        .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString()).Options);
    private static IConfiguration Config() => new ConfigurationBuilder().Build();

    [Fact]
    public async Task SameCourseSession_DoesNotMixTwoEngramsOrTwoSteps()
    {
        await using var db = CreateDb();
        var usage = new StructuredLearningEngramUsageService(db, Config());
        var session = Guid.NewGuid();
        var course = Guid.NewGuid();
        var person = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await usage.RecordTextUsageAsync(person, course, first, session, "model", 100, 20, default, "Teaching");
        await usage.RecordTextUsageAsync(person, course, second, session, "model", 300, 40, default, "Teaching");
        await usage.RecordTextUsageAsync(person, course, first, session, "model", 50, 10, default, "DeepDive");
        var stats = new StructuredLearningEngramStatsService(db, Config());
        var a = await stats.GetEngramStatsAsync(first, default);
        var b = await stats.GetEngramStatsAsync(second, default);
        a.TotalInputTokens.Should().Be(150);
        b.TotalInputTokens.Should().Be(300);
        a.StepSummaries.Single(s => s.StepType == "Teaching").TextOutputTokens.Should().Be(20);
        a.StepSummaries.Single(s => s.StepType == "DeepDive").TextOutputTokens.Should().Be(10);
        (await db.StructuredLearningEngramRuntimeUsages.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task VoiceDurationAndObserverTokens_DoNotOverwriteEachOther()
    {
        var name = Guid.NewGuid().ToString();
        await using var durationDb = CreateDb(name);
        await using var tokenDb = CreateDb(name);
        var section = Guid.NewGuid();
        var session = Guid.NewGuid();
        await Task.WhenAll(
            new StructuredLearningEngramUsageService(durationDb, Config())
                .RecordVoiceUsageAsync(null, null, section, session, "realtime", 78, 0, 0, default, "DeepDive"),
            new StructuredLearningEngramUsageService(tokenDb, Config())
                .RecordVoiceUsageAsync(null, null, section, session, "realtime", 0, 2672, 797, default, "DeepDive"));
        var stats = await new StructuredLearningEngramStatsService(durationDb, Config()).GetEngramStatsAsync(section, default);
        stats.TotalInputTokens.Should().Be(2672);
        stats.TotalOutputTokens.Should().Be(797);
        stats.Usage.Single().DurationSeconds.Should().Be(78);
        var summary = await new StructuredLearningEngramUsageService(durationDb, Config()).GetSummaryAsync(sectionId: section);
        summary.VoiceSessionCount.Should().Be(1);
    }

    [Theory]
    [InlineData("Hypothesis")]
    [InlineData("Teaching")]
    [InlineData("DeepDive")]
    [InlineData("Production")]
    [InlineData("Recall")]
    [InlineData("Assessment")]
    public async Task StepTotal_EqualsVoicePlusText_AndHistoricalUnassignedIsExplicit(string type)
    {
        await using var db = CreateDb();
        var usage = new StructuredLearningEngramUsageService(db, Config());
        var section = Guid.NewGuid();
        await usage.RecordTextUsageAsync(null, null, section, null, "text", 100, 200, default, type);
        await usage.RecordVoiceUsageAsync(null, null, section, null, "voice", 0, 300, 400, default, type);
        db.StructuredLearningEngramRuntimeUsages.Add(new StructuredLearningEngramRuntimeUsage
        {
            StructuredLearningDocumentSectionId = section, Channel = "Text",
            InputTokens = 50, OutputTokens = 60, CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var stats = await new StructuredLearningEngramStatsService(db, Config()).GetEngramStatsAsync(section, default);
        var step = stats.StepSummaries.Single(s => s.StepType == type);
        step.InputTokens.Should().Be(step.VoiceInputTokens + step.TextInputTokens).And.Be(400);
        step.OutputTokens.Should().Be(step.VoiceOutputTokens + step.TextOutputTokens).And.Be(600);
        stats.UnassignedInputTokens.Should().Be(50);
        stats.UnassignedOutputTokens.Should().Be(60);
        stats.TotalInputTokens.Should().Be(stats.StepSummaries.Sum(s => s.InputTokens) + stats.UnassignedInputTokens);
        stats.TotalOutputTokens.Should().Be(stats.StepSummaries.Sum(s => s.OutputTokens) + stats.UnassignedOutputTokens);
    }

    [Fact]
    public void Realtime_UsesTopLevelTotalsWithoutAddingAudioTextOrCacheAgain()
    {
        var seen = new HashSet<string>();
        var payload = Encoding.UTF8.GetBytes("""
            {"type":"response.done","response":{"id":"r1","usage":{
              "input_tokens":21832,"output_tokens":1877,
              "input_token_details":{"text_tokens":20000,"audio_tokens":1832,"cached_tokens":18000},
              "output_token_details":{"text_tokens":377,"audio_tokens":1500}}}}
            """);
        var logger = NullLogger.Instance;
        RealtimeVoiceUsageObserverService.ExtractUsage(payload, seen, logger).Should().Be((21832L, 1877L));
        RealtimeVoiceUsageObserverService.ExtractUsage(payload, seen, logger).Should().Be((0L, 0L));
        var second = Encoding.UTF8.GetBytes("""
            {"type":"response.done","response":{"id":"r2","usage":{"input_tokens":100,"output_tokens":25}}}
            """);
        RealtimeVoiceUsageObserverService.ExtractUsage(second, seen, logger).Should().Be((100L, 25L));
    }

    [Theory]
    [InlineData("""{"type":"response.audio.delta"}""")]
    [InlineData("""{"type":"response.done","response":{"id":"r1","usage":null}}""")]
    [InlineData("""{"type":"response.done","response":{"id":"r1","usage":{"input_tokens":-1,"output_tokens":2}}}""")]
    [InlineData("{")]
    public void NonUsageOrInvalidEvents_AreNotCounted(string json)
    {
        RealtimeVoiceUsageObserverService.ExtractUsage(Encoding.UTF8.GetBytes(json), [],
            NullLogger.Instance).Should().Be((0L, 0L));
    }
}
