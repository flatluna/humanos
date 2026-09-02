using FluentAssertions;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Models.Gamification;
using HumanOS.Models.Learning;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HumanOS.Tests.Services;

public sealed class NeuronDetailSummaryTests
{
    private static DbContextOptions<HumanOsDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 1.0)]
    [InlineData(2, 3.0)]
    [InlineData(3, 7.0)]
    [InlineData(4, 15.0)]
    [InlineData(5, 3650.0)]
    [InlineData(10, 3650.0)]
    public void MemoryScienceEngine_GetStabilityDays_MatchesCanonicalSchedule(int reviewCount, double expectedStability)
    {
        var stability = MemoryScienceEngine.GetStabilityDays(reviewCount);
        stability.Should().Be(expectedStability);
    }

    [Fact]
    public void MemoryScienceEngine_RecallCalculation_RecentReviewReports100Percent()
    {
        var now = DateTime.UtcNow;
        var recall = MemoryScienceEngine.CalculateRecallPercent(now, 1, now);
        recall.Should().Be(100);

        var state = MemoryScienceEngine.DetermineMemoryState(recall);
        state.Should().Be("Strong");
    }

    [Fact]
    public void MemoryScienceEngine_CalculateNextReviewDate_ProjectsDateCorrectly()
    {
        var baseDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        // Review count 3 -> S = 7 days. Next review at (27 / 7) * 7 = 27 days after baseDate.
        var nextDate = MemoryScienceEngine.CalculateNextReviewDate(baseDate, 3);
        nextDate.Should().NotBeNull();
        nextDate!.Value.Should().Be(baseDate.AddDays(27));
    }

    [Fact]
    public async Task GetNodeSummaryAsync_EnrichesModalWithMemoryRewardAndImpact_WhenDataExists()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var blueprintId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var sessionNodeId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);

        // 1. Setup NodeExperienceBlueprint with steps
        var blueprint = new NodeExperienceBlueprint
        {
            NodeExperienceBlueprintId = blueprintId,
            CapabilityGraphNodeId = nodeId,
            Name = "Introduction to Neural Pathways",
            Description = "Learn basic neuron structure"
        };
        dbContext.NodeExperienceBlueprints.Add(blueprint);

        var hypothesisStep = new NodeExperienceBlueprintStep
        {
            NodeExperienceBlueprintStepId = Guid.NewGuid(),
            NodeExperienceBlueprintId = blueprintId,
            StepType = ExperienceStepType.Hypothesis,
            Content = "What do you know?"
        };
        dbContext.NodeExperienceBlueprintSteps.Add(hypothesisStep);

        // 2. Setup completed LearningSession and LearningSessionNode
        var session = new LearningSession
        {
            LearningSessionId = sessionId,
            PersonId = personId,
            CapabilityId = courseId
        };
        dbContext.LearningSessions.Add(session);

        var completionTime = DateTime.UtcNow.AddDays(-1);
        var sessionNode = new LearningSessionNode
        {
            LearningSessionNodeId = sessionNodeId,
            LearningSessionId = sessionId,
            CapabilityGraphNodeId = nodeId,
            NodeExperienceBlueprintId = blueprintId,
            Status = LearningSessionNodeStatus.Completed,
            StartedDate = completionTime.AddMinutes(-30),
            CompletedDate = completionTime
        };
        dbContext.LearningSessionNodes.Add(sessionNode);

        // Passed assessment result
        dbContext.LearningAssessmentResults.Add(new LearningAssessmentResult
        {
            LearningAssessmentResultId = Guid.NewGuid(),
            LearningSessionNodeId = sessionNodeId,
            Passed = true,
            Score = 95,
            CreatedDate = completionTime
        });

        // 3. Setup RewardLedgerEntry
        dbContext.RewardLedgerEntries.Add(new RewardLedgerEntry
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            CourseId = courseId,
            EngramId = nodeId.ToString(),
            PointsBase = 65,
            DifficultyMultiplier = 1.1m,
            MemoryEffortMultiplier = 1.0m,
            SurpriseMultiplier = 1.2m,
            PointsAwarded = 86,
            WasSurprise = true,
            SurpriseType = "BONUS",
            EarnedAt = completionTime
        });

        // 4. Setup EngramImpactScore
        dbContext.EngramImpactScores.Add(new EngramImpactScore
        {
            Id = Guid.NewGuid(),
            EmployeeId = personId,
            CourseId = courseId,
            EngramId = nodeId.ToString(),
            Score = 65,
            Tier = "Important",
            PrimarySignal = "Core responsibility",
            Reason = "Essential foundational concept for job role",
            GeneratedAt = completionTime
        });

        await dbContext.SaveChangesAsync();

        var orchestrator = new InstructorRuntimeOrchestrator();

        // Act
        var summary = await orchestrator.GetNodeSummaryAsync(dbContext, personId, nodeId);

        // Assert - Old data preserved
        summary.Should().NotBeNull();
        summary.CapabilityGraphNodeId.Should().Be(nodeId);
        summary.LearningSessionNodeId.Should().Be(sessionNodeId);
        summary.AttemptCount.Should().Be(1);
        summary.FinalScore.Should().Be(95);
        summary.FirstCompletedDate.Should().BeCloseTo(completionTime, TimeSpan.FromSeconds(1));
        summary.LastCompletedDate.Should().BeCloseTo(completionTime, TimeSpan.FromSeconds(1));

        // Assert - Memory block
        summary.Memory.Should().NotBeNull();
        summary.Memory!.ReviewCount.Should().Be(1);
        summary.Memory.IsAutomatic.Should().BeFalse();
        summary.Memory.ReviewsUntilAutomatic.Should().Be(4);
        summary.Memory.RecallPercent.Should().BeGreaterThan(0);
        summary.Memory.MemoryState.Should().BeOneOf("Strong", "Fading");
        summary.Memory.LastReviewDate.Should().BeCloseTo(completionTime, TimeSpan.FromSeconds(1));
        summary.Memory.NextReviewDate.Should().NotBeNull();

        // Assert - Reward block
        summary.Reward.Should().NotBeNull();
        summary.Reward!.PointsAwarded.Should().Be(86);
        summary.Reward.PointsBase.Should().Be(65);
        summary.Reward.WasSurprise.Should().BeTrue();
        summary.Reward.SurpriseType.Should().Be("BONUS");
        summary.Reward.DifficultyMultiplier.Should().Be(1.1m);
        summary.Reward.SurpriseMultiplier.Should().Be(1.2m);

        // Assert - Impact block
        summary.Impact.Should().NotBeNull();
        summary.Impact!.Score.Should().Be(65);
        summary.Impact.Tier.Should().Be("Important");
        summary.Impact.PrimarySignal.Should().Be("Core responsibility");
        summary.Impact.Reason.Should().Be("Essential foundational concept for job role");
    }

    [Fact]
    public async Task GetNodeSummaryAsync_ReturnsNullRewardAndImpact_WhenNotEarnedOrScoredYet()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var blueprintId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var sessionNodeId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);

        var blueprint = new NodeExperienceBlueprint
        {
            NodeExperienceBlueprintId = blueprintId,
            CapabilityGraphNodeId = nodeId,
            Name = "Intro",
        };
        dbContext.NodeExperienceBlueprints.Add(blueprint);

        var session = new LearningSession
        {
            LearningSessionId = sessionId,
            PersonId = personId,
            CapabilityId = courseId
        };
        dbContext.LearningSessions.Add(session);

        var completionTime = DateTime.UtcNow.AddDays(-5);
        var sessionNode = new LearningSessionNode
        {
            LearningSessionNodeId = sessionNodeId,
            LearningSessionId = sessionId,
            CapabilityGraphNodeId = nodeId,
            NodeExperienceBlueprintId = blueprintId,
            Status = LearningSessionNodeStatus.Completed,
            CompletedDate = completionTime
        };
        dbContext.LearningSessionNodes.Add(sessionNode);
        await dbContext.SaveChangesAsync();

        var orchestrator = new InstructorRuntimeOrchestrator();

        // Act
        var summary = await orchestrator.GetNodeSummaryAsync(dbContext, personId, nodeId);

        // Assert
        summary.Should().NotBeNull();
        summary.Memory.Should().NotBeNull();
        summary.Reward.Should().BeNull();
        summary.Impact.Should().BeNull();
    }

    [Fact]
    public async Task StructuredLearningSectionSummary_CalculatesMemoryRewardAndImpact_WithMatchingEngramId()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);

        var doc = new StructuredLearningDocument
        {
            StructuredLearningDocumentId = docId,
            DocumentName = "Structured Course 1",
            TenantId = Guid.NewGuid()
        };
        dbContext.StructuredLearningDocuments.Add(doc);

        var section = new StructuredLearningDocumentSection
        {
            StructuredLearningDocumentSectionId = sectionId,
            StructuredLearningDocumentId = docId,
            SubchapterTitle = "Section 1",
            ChapterTitle = "Chapter 1",
            Capability = "Core Capability",
            SortOrder = 1
        };
        dbContext.StructuredLearningDocumentSections.Add(section);

        var roundTime = DateTime.UtcNow.AddDays(-2);
        dbContext.StructuredLearningAssessmentRounds.Add(new StructuredLearningAssessmentRound
        {
            StructuredLearningAssessmentRoundId = Guid.NewGuid(),
            StructuredLearningDocumentSectionId = sectionId,
            PersonId = personId,
            RoundNumber = 1,
            Status = StructuredLearningAssessmentRoundStatus.Passed,
            FinalScore = 100,
            CompletedDate = roundTime
        });

        // Setup RewardLedgerEntry with EngramId == sectionId.ToString()
        dbContext.RewardLedgerEntries.Add(new RewardLedgerEntry
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            CourseId = docId,
            EngramId = sectionId.ToString(),
            PointsBase = 70,
            DifficultyMultiplier = 1.0m,
            MemoryEffortMultiplier = 1.0m,
            SurpriseMultiplier = 1.5m,
            PointsAwarded = 105,
            WasSurprise = true,
            SurpriseType = "BONUS",
            EarnedAt = roundTime
        });

        // Setup EngramImpactScore with EngramId == sectionId.ToString()
        dbContext.EngramImpactScores.Add(new EngramImpactScore
        {
            Id = Guid.NewGuid(),
            EmployeeId = personId,
            CourseId = docId,
            EngramId = sectionId.ToString(),
            Score = 70,
            Tier = "Critical",
            PrimarySignal = "Direct mission alignment",
            Reason = "Vital for organizational workflows",
            GeneratedAt = roundTime
        });

        await dbContext.SaveChangesAsync();

        // Query structured section data using the exact logic in GetStructuredLearningSectionSummaryFunction
        var rounds = await dbContext.StructuredLearningAssessmentRounds
            .AsNoTracking()
            .Where(r => r.StructuredLearningDocumentSectionId == sectionId && r.PersonId == personId)
            .OrderByDescending(r => r.CompletedDate ?? r.CreatedDate)
            .ToListAsync();

        var passedRounds = rounds.Where(r => r.Status == StructuredLearningAssessmentRoundStatus.Passed).ToList();
        passedRounds.Should().HaveCount(1);

        var rewardEntry = await dbContext.RewardLedgerEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.PersonId == personId && r.CourseId == docId && r.EngramId == sectionId.ToString());
        rewardEntry.Should().NotBeNull();
        rewardEntry!.PointsAwarded.Should().Be(105);

        var impactEntry = await dbContext.EngramImpactScores
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.EmployeeId == personId && i.CourseId == docId && i.EngramId == sectionId.ToString());
        impactEntry.Should().NotBeNull();
        impactEntry!.Tier.Should().Be("Critical");

        var recallPercent = MemoryScienceEngine.CalculateRecallPercent(roundTime, 1);
        recallPercent.Should().BeGreaterThan(0);
        MemoryScienceEngine.IsAutomatic(1).Should().BeFalse();
        MemoryScienceEngine.CalculateNextReviewDate(roundTime, 1).Should().NotBeNull();
    }
}
