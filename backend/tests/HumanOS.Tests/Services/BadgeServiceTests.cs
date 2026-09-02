using FluentAssertions;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Models.Gamification;
using HumanOS.Models.Learning;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HumanOS.Tests.Services;

public sealed class BadgeServiceTests
{
    private static DbContextOptions<HumanOsDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private static async Task SeedCatalogBadgesAsync(HumanOsDbContext dbContext)
    {
        await CatalogSeeder.SeedBadgesAsync(dbContext);
    }

    [Fact]
    public async Task SeedBadgesAsync_SeedsAllRedesignedBadgesIdempotently()
    {
        var options = CreateInMemoryOptions();
        await using var dbContext = new HumanOsDbContext(options);

        // First run
        await CatalogSeeder.SeedBadgesAsync(dbContext);
        var badges = await dbContext.BadgeCatalogs.Where(b => b.IsActive).ToListAsync();
        badges.Should().HaveCount(28);
        badges.Should().Contain(b => b.Code == "first_neuron" && b.Category == "Effort" && b.RuleType == "engrams_mastered" && b.RuleThreshold == 1);
        badges.Should().Contain(b => b.Code == "effort_100" && b.Category == "Effort" && b.RuleType == "engrams_mastered" && b.RuleThreshold == 100);
        badges.Should().Contain(b => b.Code == "skill_progress_50" && b.Category == "Progress" && b.RuleType == "course_progress_pct" && b.RuleThreshold == 50);
        badges.Should().Contain(b => b.Code == "skills_3" && b.Category == "Skills" && b.RuleType == "skills_completed" && b.RuleThreshold == 3);
        badges.Should().Contain(b => b.Code == "skills_10" && b.Category == "Skills" && b.RuleType == "skills_completed" && b.RuleThreshold == 10);
        badges.Should().Contain(b => b.Code == "skills_15" && b.Category == "Skills" && b.RuleType == "skills_completed" && b.RuleThreshold == 15);
        badges.Should().Contain(b => b.Code == "quality_perfect_10" && b.Category == "Quality" && b.RuleType == "perfect_scores" && b.RuleThreshold == 10);
        badges.Should().Contain(b => b.Code == "critical_guardian" && b.Category == "Challenge" && b.RuleType == "critical_mastered" && b.RuleThreshold == 10);
        badges.Should().Contain(b => b.Code == "streak_30" && b.Category == "Habit" && b.RuleType == "streak_days" && b.RuleThreshold == 30);
        badges.Should().Contain(b => b.Code == "lucky_triple" && b.Category == "Luck" && b.RuleType == "jackpot_hit" && b.RuleThreshold == 3);

        // Second run (must not duplicate)
        await CatalogSeeder.SeedBadgesAsync(dbContext);
        var badgesAfterSecondSeed = await dbContext.BadgeCatalogs.Where(b => b.IsActive).ToListAsync();
        badgesAfterSecondSeed.Should().HaveCount(28);
    }

    [Fact]
    public async Task EvaluateAndGrantBadgesAsync_GrantsFirstNeuron_WhenOneOrMoreEngramsMastered()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 1,
            CurrentStreakDays = 1,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var awarded = await service.EvaluateAndGrantBadgesAsync(personId);

        // Assert
        awarded.Should().ContainSingle(b => b.BadgeCode == "first_neuron");
        var earnedInDb = await dbContext.PlayerBadges.Where(b => b.PersonId == personId).ToListAsync();
        earnedInDb.Should().ContainSingle(b => b.BadgeCode == "first_neuron");
    }

    [Fact]
    public async Task EvaluateAndGrantBadgesAsync_GrantsStreak7_WhenStreakIsSevenOrMore()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 0,
            CurrentStreakDays = 7,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var awarded = await service.EvaluateAndGrantBadgesAsync(personId);

        // Assert
        awarded.Should().ContainSingle(b => b.BadgeCode == "streak_7");
        var earnedInDb = await dbContext.PlayerBadges.Where(b => b.PersonId == personId).ToListAsync();
        earnedInDb.Should().ContainSingle(b => b.BadgeCode == "streak_7");
    }

    [Fact]
    public async Task EvaluateAndGrantBadgesAsync_GrantsCriticalGuardian_WhenTenCriticalEngramsMastered()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 10,
            CurrentStreakDays = 2,
            UpdatedAt = DateTime.UtcNow
        });

        for (int i = 1; i <= 10; i++)
        {
            var engramId = $"engram-crit-{i}";
            dbContext.EngramImpactScores.Add(new EngramImpactScore
            {
                Id = Guid.NewGuid(),
                EmployeeId = personId,
                CourseId = courseId,
                EngramId = engramId,
                Score = 90,
                Tier = "Critical",
                GeneratedAt = DateTime.UtcNow
            });

            dbContext.RewardLedgerEntries.Add(new RewardLedgerEntry
            {
                Id = Guid.NewGuid(),
                PersonId = personId,
                CourseId = courseId,
                EngramId = engramId,
                PointsBase = 90,
                PointsAwarded = 90,
                EarnedAt = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var awarded = await service.EvaluateAndGrantBadgesAsync(personId);

        // Assert
        awarded.Should().Contain(b => b.BadgeCode == "critical_guardian");
        awarded.Should().Contain(b => b.BadgeCode == "first_neuron"); // Since total mastered is 10 >= 1
    }

    [Fact]
    public async Task EvaluateAndGrantBadgesAsync_GrantsLuckyJackpot_WhenJackpotWasHit()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var courseId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 1,
            CurrentStreakDays = 1,
            UpdatedAt = DateTime.UtcNow
        });

        dbContext.RewardLedgerEntries.Add(new RewardLedgerEntry
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            CourseId = courseId,
            EngramId = "engram-lucky",
            PointsBase = 60,
            SurpriseMultiplier = 3.0m,
            SurpriseType = "JACKPOT",
            WasSurprise = true,
            PointsAwarded = 180,
            EarnedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var awarded = await service.EvaluateAndGrantBadgesAsync(personId);

        // Assert
        awarded.Should().Contain(b => b.BadgeCode == "lucky_jackpot");
    }

    [Fact]
    public async Task EvaluateAndGrantBadgesAsync_IsIdempotent_DoesNotDuplicateBadgesOnSecondRun()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 5,
            CurrentStreakDays = 7,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act 1: First run grants first_neuron and streak_7
        var firstRun = await service.EvaluateAndGrantBadgesAsync(personId);
        firstRun.Should().HaveCount(2);
        firstRun.Should().Contain(b => b.BadgeCode == "first_neuron");
        firstRun.Should().Contain(b => b.BadgeCode == "streak_7");

        // Act 2: Second run without any progress changes
        var secondRun = await service.EvaluateAndGrantBadgesAsync(personId);

        // Assert
        secondRun.Should().BeEmpty();
        var allPlayerBadges = await dbContext.PlayerBadges.Where(b => b.PersonId == personId).ToListAsync();
        allPlayerBadges.Should().HaveCount(2);
    }

    [Fact]
    public async Task EvaluateAndGrantBadgesAsync_GrantsCourseProgressPct_WhenThresholdReached()
    {
        // Course has 4 sections, student mastered 2 (50%) -> awards skill_progress_25 and skill_progress_50
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 2,
            CurrentStreakDays = 1,
            UpdatedAt = DateTime.UtcNow
        });

        var doc = new StructuredLearningDocument
        {
            StructuredLearningDocumentId = docId,
            DocumentName = "Structured Course 1",
            TenantId = Guid.NewGuid(),
            IsPublished = true
        };
        dbContext.StructuredLearningDocuments.Add(doc);

        for (int i = 0; i < 4; i++)
        {
            var sectionId = Guid.NewGuid();
            dbContext.StructuredLearningDocumentSections.Add(new StructuredLearningDocumentSection
            {
                StructuredLearningDocumentSectionId = sectionId,
                StructuredLearningDocumentId = docId,
                SubchapterTitle = $"Section {i + 1}",
                SortOrder = i
            });

            if (i < 2) // Master 2 of 4 = 50%
            {
                dbContext.StructuredLearningAssessmentRounds.Add(new StructuredLearningAssessmentRound
                {
                    StructuredLearningAssessmentRoundId = Guid.NewGuid(),
                    StructuredLearningDocumentSectionId = sectionId,
                    PersonId = personId,
                    RoundNumber = 1,
                    Status = StructuredLearningAssessmentRoundStatus.Passed,
                    FinalScore = 90
                });
            }
        }
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var awarded = await service.EvaluateAndGrantBadgesAsync(personId);

        // Assert
        awarded.Should().Contain(b => b.BadgeCode == "skill_progress_25");
        awarded.Should().Contain(b => b.BadgeCode == "skill_progress_50");
        awarded.Should().NotContain(b => b.BadgeCode == "skill_progress_75");
        awarded.Should().NotContain(b => b.BadgeCode == "skill_complete_1");
    }

    [Fact]
    public async Task EvaluateAndGrantBadgesAsync_GrantsSkillsCompleted_WhenThreeCoursesCompleted()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 6,
            CurrentStreakDays = 1,
            UpdatedAt = DateTime.UtcNow
        });

        // Add 3 structured documents with 2 sections each, all 100% completed
        for (int c = 1; c <= 3; c++)
        {
            var docId = Guid.NewGuid();
            dbContext.StructuredLearningDocuments.Add(new StructuredLearningDocument
            {
                StructuredLearningDocumentId = docId,
                DocumentName = $"Course {c}",
                TenantId = Guid.NewGuid(),
                IsPublished = true
            });

            for (int s = 1; s <= 2; s++)
            {
                var sectionId = Guid.NewGuid();
                dbContext.StructuredLearningDocumentSections.Add(new StructuredLearningDocumentSection
                {
                    StructuredLearningDocumentSectionId = sectionId,
                    StructuredLearningDocumentId = docId,
                    SubchapterTitle = $"Sec {s}",
                    SortOrder = s
                });

                dbContext.StructuredLearningAssessmentRounds.Add(new StructuredLearningAssessmentRound
                {
                    StructuredLearningAssessmentRoundId = Guid.NewGuid(),
                    StructuredLearningDocumentSectionId = sectionId,
                    PersonId = personId,
                    RoundNumber = 1,
                    Status = StructuredLearningAssessmentRoundStatus.Passed,
                    FinalScore = 100
                });
            }
        }
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var awarded = await service.EvaluateAndGrantBadgesAsync(personId);

        // Assert
        awarded.Should().Contain(b => b.BadgeCode == "skill_complete_1");
        awarded.Should().Contain(b => b.BadgeCode == "skills_1");
        awarded.Should().Contain(b => b.BadgeCode == "skills_3");
        awarded.Should().NotContain(b => b.BadgeCode == "skills_6");
    }

    [Fact]
    public async Task EvaluateAndGrantBadgesAsync_GrantsPerfectScoresAndFirstTryMasteries()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 10,
            CurrentStreakDays = 1,
            UpdatedAt = DateTime.UtcNow
        });

        var docId = Guid.NewGuid();
        dbContext.StructuredLearningDocuments.Add(new StructuredLearningDocument
        {
            StructuredLearningDocumentId = docId,
            DocumentName = "Doc Perfect",
            TenantId = Guid.NewGuid()
        });

        for (int i = 1; i <= 10; i++)
        {
            var sectionId = Guid.NewGuid();
            dbContext.StructuredLearningDocumentSections.Add(new StructuredLearningDocumentSection
            {
                StructuredLearningDocumentSectionId = sectionId,
                StructuredLearningDocumentId = docId,
                SubchapterTitle = $"Section {i}"
            });

            dbContext.StructuredLearningAssessmentRounds.Add(new StructuredLearningAssessmentRound
            {
                StructuredLearningAssessmentRoundId = Guid.NewGuid(),
                StructuredLearningDocumentSectionId = sectionId,
                PersonId = personId,
                RoundNumber = 1, // First try
                Status = StructuredLearningAssessmentRoundStatus.Passed,
                FinalScore = 100, // Perfect score
                CompletedDate = DateTime.UtcNow.AddMinutes(i)
            });
        }
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var awarded = await service.EvaluateAndGrantBadgesAsync(personId);

        // Assert
        awarded.Should().Contain(b => b.BadgeCode == "quality_perfect_10");
        awarded.Should().Contain(b => b.BadgeCode == "quality_firsttry_10");
        awarded.Should().Contain(b => b.BadgeCode == "quality_flawless_5");
    }

    [Fact]
    public async Task EvaluateAndGrantBadgesAsync_GrantsAutomaticPilot_WhenFiveEngramsReachAutomaticStatus()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 5,
            CurrentStreakDays = 1,
            UpdatedAt = DateTime.UtcNow
        });

        // 5 distinct structured sections, each with 5 passed rounds (>= 5 = Automatic)
        for (int s = 1; s <= 5; s++)
        {
            var sectionId = Guid.NewGuid();
            for (int r = 1; r <= 5; r++)
            {
                dbContext.StructuredLearningAssessmentRounds.Add(new StructuredLearningAssessmentRound
                {
                    StructuredLearningAssessmentRoundId = Guid.NewGuid(),
                    StructuredLearningDocumentSectionId = sectionId,
                    PersonId = personId,
                    RoundNumber = r,
                    Status = StructuredLearningAssessmentRoundStatus.Passed,
                    FinalScore = 100
                });
            }
        }
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var awarded = await service.EvaluateAndGrantBadgesAsync(personId);

        // Assert
        awarded.Should().Contain(b => b.BadgeCode == "automatic_pilot");
    }

    [Fact]
    public async Task EvaluateAndGrantBadgesAsync_DoesNotGrantBadge_WhenRuleThresholdNotMet()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        // Streak is 6 (below 7), Mastered is 0 (below 1)
        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 0,
            CurrentStreakDays = 6,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var awarded = await service.EvaluateAndGrantBadgesAsync(personId);

        // Assert
        awarded.Should().BeEmpty();
        var inDb = await dbContext.PlayerBadges.Where(b => b.PersonId == personId).ToListAsync();
        inDb.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBadgeProgressAsync_NearCompletionBadge_ReportsAccurateProgressAndPercentage()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 22,
            CurrentStreakDays = 3,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var progress = await service.GetBadgeProgressAsync(personId);

        // Assert
        progress.Should().NotBeEmpty();

        var effort25 = progress.FirstOrDefault(p => p.Code == "effort_25");
        effort25.Should().NotBeNull();
        effort25!.CurrentValue.Should().Be(22);
        effort25.TargetValue.Should().Be(25);
        effort25.ProgressPercent.Should().Be(88); // 22/25 = 88%
        effort25.IsEarned.Should().BeFalse();

        var streak7 = progress.FirstOrDefault(p => p.Code == "streak_7");
        streak7.Should().NotBeNull();
        streak7!.CurrentValue.Should().Be(3);
        streak7.TargetValue.Should().Be(7);
        streak7.ProgressPercent.Should().Be(43); // 3/7 = 42.8% -> 43%
        streak7.IsEarned.Should().BeFalse();
    }

    [Fact]
    public async Task GetBadgeProgressAsync_EarnedBadge_ReportsIsEarnedTrueAnd100Percent()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var earnedTime = DateTime.UtcNow.AddDays(-2);

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 5,
            CurrentStreakDays = 2,
            UpdatedAt = DateTime.UtcNow
        });

        dbContext.PlayerBadges.Add(new PlayerBadge
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            BadgeCode = "first_neuron",
            EarnedAt = earnedTime
        });
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var progress = await service.GetBadgeProgressAsync(personId);

        // Assert
        var firstNeuron = progress.FirstOrDefault(p => p.Code == "first_neuron");
        firstNeuron.Should().NotBeNull();
        firstNeuron!.IsEarned.Should().BeTrue();
        firstNeuron.EarnedAt.Should().BeCloseTo(earnedTime, TimeSpan.FromSeconds(1));
        firstNeuron.ProgressPercent.Should().Be(100);
        firstNeuron.CurrentValue.Should().Be(5);
        firstNeuron.TargetValue.Should().Be(1);
    }

    [Fact]
    public async Task GetBadgeProgressAsync_UnearnedBadges_AreOrderedByProgressPercentDescending()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var earnedTime = DateTime.UtcNow.AddDays(-1);

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 22,
            CurrentStreakDays = 3,
            UpdatedAt = DateTime.UtcNow
        });

        dbContext.PlayerBadges.Add(new PlayerBadge
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            BadgeCode = "first_neuron",
            EarnedAt = earnedTime
        });
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var progress = await service.GetBadgeProgressAsync(personId);

        // Assert
        // Earned badges must be at the very top
        var earned = progress.Where(p => p.IsEarned).ToList();
        earned.Should().ContainSingle(p => p.Code == "first_neuron");
        progress.First().Code.Should().Be("first_neuron");

        // Unearned badges must follow in descending order of ProgressPercent
        var unearned = progress.Where(p => !p.IsEarned).ToList();
        unearned.Should().NotBeEmpty();

        for (int i = 0; i < unearned.Count - 1; i++)
        {
            unearned[i].ProgressPercent.Should().BeGreaterThanOrEqualTo(
                unearned[i + 1].ProgressPercent,
                $"Badge at index {i} ({unearned[i].Code} at {unearned[i].ProgressPercent}%) should have >= progress than {unearned[i + 1].Code} at {unearned[i + 1].ProgressPercent}%");
        }

        // effort_25 (88%) should appear before streak_7 (43%)
        var effort25Index = unearned.FindIndex(p => p.Code == "effort_25");
        var streak7Index = unearned.FindIndex(p => p.Code == "streak_7");
        var effort300Index = unearned.FindIndex(p => p.Code == "effort_300"); // 22/300 = 7%

        effort25Index.Should().BeLessThan(streak7Index);
        streak7Index.Should().BeLessThan(effort300Index);
    }

    [Fact]
    public async Task GetBadgeProgressAsync_AutomaticEngrams_ReportsAccurateCurrentValueAndPercent()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 50,
            CurrentStreakDays = 15,
            UpdatedAt = DateTime.UtcNow
        });

        // 2 distinct sections with 5 passed rounds each -> 2 automatic engrams
        for (int s = 1; s <= 2; s++)
        {
            var sectionId = Guid.NewGuid();
            for (int r = 1; r <= 5; r++)
            {
                dbContext.StructuredLearningAssessmentRounds.Add(new StructuredLearningAssessmentRound
                {
                    StructuredLearningAssessmentRoundId = Guid.NewGuid(),
                    StructuredLearningDocumentSectionId = sectionId,
                    PersonId = personId,
                    RoundNumber = r,
                    Status = StructuredLearningAssessmentRoundStatus.Passed,
                    FinalScore = 100
                });
            }
        }

        // 1 section with only 3 passed rounds (< 5 -> NOT automatic)
        var nonAutoSectionId = Guid.NewGuid();
        for (int r = 1; r <= 3; r++)
        {
            dbContext.StructuredLearningAssessmentRounds.Add(new StructuredLearningAssessmentRound
            {
                StructuredLearningAssessmentRoundId = Guid.NewGuid(),
                StructuredLearningDocumentSectionId = nonAutoSectionId,
                PersonId = personId,
                RoundNumber = r,
                Status = StructuredLearningAssessmentRoundStatus.Passed,
                FinalScore = 100
            });
        }
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var progress = await service.GetBadgeProgressAsync(personId);

        // Assert
        var autoPilot = progress.FirstOrDefault(p => p.Code == "automatic_pilot");
        autoPilot.Should().NotBeNull();
        autoPilot!.RuleType.Should().Be("automatic_engrams");
        autoPilot.CurrentValue.Should().Be(2); // Exactly 2 distinct automatic engrams
        autoPilot.TargetValue.Should().Be(5);
        autoPilot.ProgressPercent.Should().Be(40); // 2/5 = 40%
        autoPilot.IsEarned.Should().BeFalse();
    }

    [Fact]
    public async Task GetBadgeProgressAsync_CourseProgressPct_ReportsMaxCourseProgressAcrossCourses()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var course1Id = Guid.NewGuid();
        var course2Id = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 4,
            CurrentStreakDays = 1,
            UpdatedAt = DateTime.UtcNow
        });

        // Course 1: 4 sections, 1 mastered = 25%
        dbContext.StructuredLearningDocuments.Add(new StructuredLearningDocument
        {
            StructuredLearningDocumentId = course1Id,
            DocumentName = "Course 1",
            TenantId = Guid.NewGuid()
        });
        for (int i = 0; i < 4; i++)
        {
            var secId = Guid.NewGuid();
            dbContext.StructuredLearningDocumentSections.Add(new StructuredLearningDocumentSection
            {
                StructuredLearningDocumentSectionId = secId,
                StructuredLearningDocumentId = course1Id,
                SubchapterTitle = $"C1 Sec {i}"
            });
            if (i == 0)
            {
                dbContext.StructuredLearningAssessmentRounds.Add(new StructuredLearningAssessmentRound
                {
                    StructuredLearningAssessmentRoundId = Guid.NewGuid(),
                    StructuredLearningDocumentSectionId = secId,
                    PersonId = personId,
                    RoundNumber = 1,
                    Status = StructuredLearningAssessmentRoundStatus.Passed
                });
            }
        }

        // Course 2: 4 sections, 3 mastered = 75%
        dbContext.StructuredLearningDocuments.Add(new StructuredLearningDocument
        {
            StructuredLearningDocumentId = course2Id,
            DocumentName = "Course 2",
            TenantId = Guid.NewGuid()
        });
        for (int i = 0; i < 4; i++)
        {
            var secId = Guid.NewGuid();
            dbContext.StructuredLearningDocumentSections.Add(new StructuredLearningDocumentSection
            {
                StructuredLearningDocumentSectionId = secId,
                StructuredLearningDocumentId = course2Id,
                SubchapterTitle = $"C2 Sec {i}"
            });
            if (i < 3)
            {
                dbContext.StructuredLearningAssessmentRounds.Add(new StructuredLearningAssessmentRound
                {
                    StructuredLearningAssessmentRoundId = Guid.NewGuid(),
                    StructuredLearningDocumentSectionId = secId,
                    PersonId = personId,
                    RoundNumber = 1,
                    Status = StructuredLearningAssessmentRoundStatus.Passed
                });
            }
        }
        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var progress = await service.GetBadgeProgressAsync(personId);

        // Assert
        // Max course completion pct is 75% (from Course 2)
        var progress50 = progress.FirstOrDefault(p => p.Code == "skill_progress_50");
        progress50.Should().NotBeNull();
        progress50!.CurrentValue.Should().Be(75);
        progress50.TargetValue.Should().Be(50);
        progress50.ProgressPercent.Should().Be(100); // 75/50 clamped to 100%

        var progress75 = progress.FirstOrDefault(p => p.Code == "skill_progress_75");
        progress75.Should().NotBeNull();
        progress75!.CurrentValue.Should().Be(75);
        progress75.TargetValue.Should().Be(75);
        progress75.ProgressPercent.Should().Be(100);
    }

    [Fact]
    public async Task GetBadgeProgressAsync_WhenNoPlayerProfile_ReturnsActiveBadgesWithZeroProgress()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        var service = new BadgeService(dbContext);

        // Act
        var progress = await service.GetBadgeProgressAsync(personId);

        // Assert
        progress.Should().HaveCount(28);
        progress.Should().OnlyContain(p => !p.IsEarned && p.CurrentValue == 0 && p.ProgressPercent == 0 && p.EarnedAt == null);
    }

    [Fact]
    public async Task GetTotalAssignedEngramsAsync_CalculatesTotalAndDeduplicatesAcrossSources()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var graphCapId = Guid.NewGuid();
        var structDocId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);

        // 1. Graph Capability with 3 nodes
        var graphCap = new HumanOS.Models.Capabilities.Capability
        {
            CapabilityId = graphCapId,
            Code = "GRAPH-CAP-1",
            Name = "Graph Capability",
            IsActive = true
        };
        dbContext.Capabilities.Add(graphCap);

        var graph = new HumanOS.Models.Capabilities.Graph.CapabilityGraph
        {
            CapabilityGraphId = Guid.NewGuid(),
            CapabilityId = graphCapId
        };
        dbContext.CapabilityGraphs.Add(graph);

        for (int i = 0; i < 3; i++)
        {
            dbContext.CapabilityGraphNodes.Add(new HumanOS.Models.Capabilities.Graph.CapabilityGraphNode
            {
                CapabilityGraphNodeId = Guid.NewGuid(),
                CapabilityGraphId = graph.CapabilityGraphId,
                Name = $"Graph Node {i + 1}"
            });
        }

        // 2. Structured Document with 4 sections
        var structDoc = new StructuredLearningDocument
        {
            StructuredLearningDocumentId = structDocId,
            DocumentName = "Structured Document",
            TenantId = Guid.NewGuid(),
            IsPublished = true
        };
        dbContext.StructuredLearningDocuments.Add(structDoc);

        for (int i = 0; i < 4; i++)
        {
            dbContext.StructuredLearningDocumentSections.Add(new StructuredLearningDocumentSection
            {
                StructuredLearningDocumentSectionId = Guid.NewGuid(),
                StructuredLearningDocumentId = structDocId,
                SubchapterTitle = $"Section {i + 1}"
            });
        }

        // 3. PersonCareerPlan assigning graphCapId
        var roadmapJson = $"[{{\"MatchedCapabilityId\":\"{graphCapId}\"}}]";
        dbContext.PersonCareerPlans.Add(new HumanOS.Models.People.PersonCareerPlan
        {
            PersonCareerPlanId = Guid.NewGuid(),
            PersonId = personId,
            TechnicalRoadmapJson = roadmapJson
        });

        // 4. Enrollments: graphCapId duplicated + structDocId enrolled
        dbContext.CapabilityEnrollments.Add(new HumanOS.Models.Capabilities.CapabilityEnrollment
        {
            CapabilityEnrollmentId = Guid.NewGuid(),
            PersonId = personId,
            CapabilityId = graphCapId
        });

        dbContext.StructuredLearningDocumentEnrollments.Add(new HumanOS.Models.Capabilities.Studio.StructuredLearningDocumentEnrollment
        {
            StructuredLearningDocumentEnrollmentId = Guid.NewGuid(),
            PersonId = personId,
            StructuredLearningDocumentId = structDocId
        });

        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var total = await service.GetTotalAssignedEngramsAsync(personId);
        var breakdown = await service.GetAssignedEngramsBreakdownAsync(personId);
        var totalCourses = await service.GetTotalAssignedCoursesAsync(personId);
        var completedCourses = await service.GetCompletedCoursesCountAsync(personId);

        // Assert: 3 graph nodes + 4 structured sections = 7 total assigned engrams (graphCapId deduplicated)
        total.Should().Be(7);
        breakdown.TotalAssignedEngrams.Should().Be(7);
        breakdown.TotalCourses.Should().Be(2);
        breakdown.GraphCoursesCount.Should().Be(1);
        breakdown.GraphEngramsCount.Should().Be(3);
        breakdown.StructuredCoursesCount.Should().Be(1);
        breakdown.StructuredEngramsCount.Should().Be(4);
        totalCourses.Should().Be(2);
        completedCourses.Should().Be(0);
    }

    [Fact]
    public async Task GetTotalAssignedCoursesAsync_CountsAllRoadmapSkills_EvenWhenNotGeneratedYet()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);

        // Career plan with 5 ungenerated skills across technical and soft roadmaps (no MatchedCapabilityId)
        var techJson = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { Skill = "Skill A", Title = "Title A", MatchedCapabilityId = (string?)null },
            new { Skill = "Skill B", Title = "Title B", MatchedCapabilityId = (string?)null },
            new { Skill = "Skill C", Title = "Title C", MatchedCapabilityId = (string?)null }
        });

        var softJson = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { Skill = "Skill D", Title = "Title D", MatchedCapabilityId = (string?)null },
            new { Skill = "Skill E", Title = "Title E", MatchedCapabilityId = (string?)null }
        });

        dbContext.PersonCareerPlans.Add(new HumanOS.Models.People.PersonCareerPlan
        {
            PersonCareerPlanId = Guid.NewGuid(),
            PersonId = personId,
            TechnicalRoadmapJson = techJson,
            SoftSkillsRoadmapJson = softJson
        });

        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var totalAssignedCourses = await service.GetTotalAssignedCoursesAsync(personId);
        var completedCourses = await service.GetCompletedCoursesCountAsync(personId);

        // Assert: All 5 ungenerated skills are counted as 5 courses
        totalAssignedCourses.Should().Be(5);
        completedCourses.Should().Be(0);
    }

    [Fact]
    public async Task DynamicEffortBadges_CalculateThresholdsAndDescriptionsAccurately_FromTotalAvailableEngrams()
    {
        // Setup a person with 41 total available engrams (like Alejandro) and 2 engrams mastered
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 2,
            CurrentStreakDays = 1,
            UpdatedAt = DateTime.UtcNow
        });

        // 1 Structured Learning document with 41 sections
        var doc = new StructuredLearningDocument
        {
            StructuredLearningDocumentId = docId,
            DocumentName = "API + MICROSERVICES",
            TenantId = Guid.NewGuid(),
            IsPublished = true
        };
        dbContext.StructuredLearningDocuments.Add(doc);

        for (int i = 0; i < 41; i++)
        {
            dbContext.StructuredLearningDocumentSections.Add(new StructuredLearningDocumentSection
            {
                StructuredLearningDocumentSectionId = Guid.NewGuid(),
                StructuredLearningDocumentId = docId,
                SubchapterTitle = $"Section {i + 1}"
            });
        }

        dbContext.StructuredLearningDocumentEnrollments.Add(new HumanOS.Models.Capabilities.Studio.StructuredLearningDocumentEnrollment
        {
            StructuredLearningDocumentEnrollmentId = Guid.NewGuid(),
            PersonId = personId,
            StructuredLearningDocumentId = docId
        });

        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var progress = await service.GetBadgeProgressAsync(personId);

        // Assert - Effort Badges Curve:
        // total = 41
        // Apprentice  (effort_10):  5%  -> ceil(41 * 0.05) = ceil(2.05)  = 3  -> 2/3 = 67%
        // Practitioner(effort_25): 15%  -> ceil(41 * 0.15) = ceil(6.15)  = 7  -> 2/7 = 29%
        // Expert      (effort_50): 30%  -> ceil(41 * 0.30) = ceil(12.3)  = 13 -> 2/13 = 15%
        // Master     (effort_100): 50%  -> ceil(41 * 0.50) = ceil(20.5)  = 21 -> 2/21 = 10%
        // Scholar    (effort_200): 75%  -> ceil(41 * 0.75) = ceil(30.75) = 31 -> 2/31 = 6%
        // Legend     (effort_300): 100% -> ceil(41 * 1.00) = 41          = 41 -> 2/41 = 5%

        var effort10 = progress.First(p => p.Code == "effort_10");
        effort10.TargetValue.Should().Be(3);
        effort10.CurrentValue.Should().Be(2);
        effort10.ProgressPercent.Should().Be(67);
        effort10.Description.Should().Be("Master 5% of your available engrams (3 of 41).");

        var effort25 = progress.First(p => p.Code == "effort_25");
        effort25.TargetValue.Should().Be(7);
        effort25.CurrentValue.Should().Be(2);
        effort25.ProgressPercent.Should().Be(29);
        effort25.Description.Should().Be("Master 15% of your available engrams (7 of 41).");

        var effort50 = progress.First(p => p.Code == "effort_50");
        effort50.TargetValue.Should().Be(13);
        effort50.CurrentValue.Should().Be(2);
        effort50.ProgressPercent.Should().Be(15);
        effort50.Description.Should().Be("Master 30% of your available engrams (13 of 41).");

        var effort100 = progress.First(p => p.Code == "effort_100");
        effort100.TargetValue.Should().Be(21);
        effort100.CurrentValue.Should().Be(2);
        effort100.ProgressPercent.Should().Be(10);
        effort100.Description.Should().Be("Master 50% of your available engrams (21 of 41).");

        var effort200 = progress.First(p => p.Code == "effort_200");
        effort200.TargetValue.Should().Be(31);
        effort200.CurrentValue.Should().Be(2);
        effort200.ProgressPercent.Should().Be(6);
        effort200.Description.Should().Be("Master 75% of your available engrams (31 of 41).");

        var effort300 = progress.First(p => p.Code == "effort_300");
        effort300.TargetValue.Should().Be(41);
        effort300.CurrentValue.Should().Be(2);
        effort300.ProgressPercent.Should().Be(5);
        effort300.Description.Should().Be("Master 100% of your available engrams (41 of 41).");
    }

    [Fact]
    public async Task DynamicSkillsBadges_CalculateThresholdsAndDescriptionsAccurately_FromTotalAssignedCourses()
    {
        // Setup a person with 20 assigned career courses (like Alejandro) and 0 completed courses
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);
        await SeedCatalogBadgesAsync(dbContext);

        dbContext.PlayerProfiles.Add(new PlayerProfile
        {
            Id = Guid.NewGuid(),
            PersonId = personId,
            TotalEngramsMastered = 2,
            CurrentStreakDays = 1,
            UpdatedAt = DateTime.UtcNow
        });

        // 10 technical + 10 soft skills = 20 career roadmap courses
        var techList = new List<object>();
        for (int i = 1; i <= 10; i++) techList.Add(new { Skill = $"Tech Skill {i}", Title = $"Tech Course {i}" });

        var softList = new List<object>();
        for (int i = 1; i <= 10; i++) softList.Add(new { Skill = $"Soft Skill {i}", Title = $"Soft Course {i}" });

        dbContext.PersonCareerPlans.Add(new HumanOS.Models.People.PersonCareerPlan
        {
            PersonCareerPlanId = Guid.NewGuid(),
            PersonId = personId,
            TechnicalRoadmapJson = System.Text.Json.JsonSerializer.Serialize(techList),
            SoftSkillsRoadmapJson = System.Text.Json.JsonSerializer.Serialize(softList)
        });

        await dbContext.SaveChangesAsync();

        var service = new BadgeService(dbContext);

        // Act
        var progress = await service.GetBadgeProgressAsync(personId);

        // Assert - Skills Badges Scale over 20 courses:
        // First Skill       (skills_1):   5%  -> ceil(20 * 0.05) = 1  -> 0/1 = 0%
        // Getting Started   (skills_3):  15%  -> ceil(20 * 0.15) = 3  -> 0/3 = 0%
        // Building Momentum (skills_6):  30%  -> ceil(20 * 0.30) = 6  -> 0/6 = 0%
        // Halfway Expert    (skills_10): 50%  -> ceil(20 * 0.50) = 10 -> 0/10 = 0%
        // Almost There      (skills_15): 75%  -> ceil(20 * 0.75) = 15 -> 0/15 = 0%
        // Skill Master      (skills_all):100% -> ceil(20 * 1.00) = 20 -> 0/20 = 0%

        var skills1 = progress.First(p => p.Code == "skills_1");
        skills1.TargetValue.Should().Be(1);
        skills1.CurrentValue.Should().Be(0);
        skills1.ProgressPercent.Should().Be(0);
        skills1.Description.Should().Be("Complete 5% of your career courses (1 of 20).");

        var skills3 = progress.First(p => p.Code == "skills_3");
        skills3.TargetValue.Should().Be(3);
        skills3.CurrentValue.Should().Be(0);
        skills3.ProgressPercent.Should().Be(0);
        skills3.Description.Should().Be("Complete 15% of your career courses (3 of 20).");

        var skills6 = progress.First(p => p.Code == "skills_6");
        skills6.TargetValue.Should().Be(6);
        skills6.CurrentValue.Should().Be(0);
        skills6.ProgressPercent.Should().Be(0);
        skills6.Description.Should().Be("Complete 30% of your career courses (6 of 20).");

        var skills10 = progress.First(p => p.Code == "skills_10");
        skills10.TargetValue.Should().Be(10);
        skills10.CurrentValue.Should().Be(0);
        skills10.ProgressPercent.Should().Be(0);
        skills10.Description.Should().Be("Complete 50% of your career courses (10 of 20).");

        var skills15 = progress.First(p => p.Code == "skills_15");
        skills15.TargetValue.Should().Be(15);
        skills15.CurrentValue.Should().Be(0);
        skills15.ProgressPercent.Should().Be(0);
        skills15.Description.Should().Be("Complete 75% of your career courses (15 of 20).");

        var skillsAll = progress.First(p => p.Code == "skills_all");
        skillsAll.TargetValue.Should().Be(20);
        skillsAll.CurrentValue.Should().Be(0);
        skillsAll.ProgressPercent.Should().Be(0);
        skillsAll.Description.Should().Be("Complete 100% of your career courses (20 of 20).");
    }
}
