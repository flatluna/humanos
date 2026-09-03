using FluentAssertions;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Models.Learning;
using HumanOS.Models.OrgStructure;
using HumanOS.Models.People;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HumanOS.Tests.Services;

public sealed class ReviewQueueServiceTests
{
    private static DbContextOptions<HumanOsDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public async Task GetReviewQueueAsync_IdentifiesPhase1_WhenUnder80PercentReadyCoursesCompleted()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);

        // Setup Person with JobRole
        dbContext.People.Add(new Person
        {
            PersonId = personId,
            JobRoleId = roleId,
            AzureOid = "oid-1",
            AzureTid = "tid-1",
            Email = "student@example.com"
        });

        // Add 2 skills to JobRole
        var skill1 = new Skill { SkillId = Guid.NewGuid(), Name = "Course Alpha", Category = "job-skills", TenantId = Guid.NewGuid() };
        var skill2 = new Skill { SkillId = Guid.NewGuid(), Name = "Course Beta", Category = "job-skills", TenantId = Guid.NewGuid() };
        dbContext.Skills.AddRange(skill1, skill2);
        dbContext.JobRoleSkills.AddRange(
            new JobRoleSkill { JobRoleId = roleId, SkillId = skill1.SkillId },
            new JobRoleSkill { JobRoleId = roleId, SkillId = skill2.SkillId }
        );

        // Add matching StructuredLearningDocuments (ready courses)
        var doc1 = new StructuredLearningDocument { StructuredLearningDocumentId = Guid.NewGuid(), DocumentName = "Course Alpha", TargetSkill = "Course Alpha" };
        var doc2 = new StructuredLearningDocument { StructuredLearningDocumentId = Guid.NewGuid(), DocumentName = "Course Beta", TargetSkill = "Course Beta" };
        dbContext.StructuredLearningDocuments.AddRange(doc1, doc2);

        // Add 2 sections to doc1 and 2 to doc2
        var s1 = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = doc1.StructuredLearningDocumentId, SubchapterTitle = "Alpha Section 1" };
        var s2 = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = doc1.StructuredLearningDocumentId, SubchapterTitle = "Alpha Section 2" };
        var s3 = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = doc2.StructuredLearningDocumentId, SubchapterTitle = "Beta Section 1" };
        var s4 = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = doc2.StructuredLearningDocumentId, SubchapterTitle = "Beta Section 2" };
        dbContext.StructuredLearningDocumentSections.AddRange(s1, s2, s3, s4);

        await dbContext.SaveChangesAsync();

        var badgeService = new BadgeService(dbContext);
        var reviewService = new ReviewQueueService(dbContext, badgeService, NullLogger<ReviewQueueService>.Instance);

        var result = await reviewService.GetReviewQueueAsync(personId);

        result.Phase.Should().Be("Phase1");
        result.PhaseName.Should().Be("Learning");
        result.ReadyCoursesCompletionPct.Should().Be(0.0);
        result.TotalPending.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReviewQueueAsync_Phase1_FiltersOnlyCriticalWithRecallUnder60()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);

        // Setup Person with JobRole
        dbContext.People.Add(new Person
        {
            PersonId = personId,
            JobRoleId = roleId,
            AzureOid = "oid-1",
            AzureTid = "tid-1",
            Email = "student@example.com"
        });

        var skill = new Skill { SkillId = Guid.NewGuid(), Name = "API Mastery", Category = "job-skills", TenantId = Guid.NewGuid() };
        dbContext.Skills.Add(skill);
        dbContext.JobRoleSkills.Add(new JobRoleSkill { JobRoleId = roleId, SkillId = skill.SkillId });

        var doc = new StructuredLearningDocument { StructuredLearningDocumentId = Guid.NewGuid(), DocumentName = "API Mastery", TargetSkill = "API Mastery" };
        dbContext.StructuredLearningDocuments.Add(doc);

        var secCriticalFading = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = doc.StructuredLearningDocumentId, SubchapterTitle = "Critical Fading Node" };
        var secCriticalFresh = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = doc.StructuredLearningDocumentId, SubchapterTitle = "Critical Fresh Node" };
        var secImportantFading = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = doc.StructuredLearningDocumentId, SubchapterTitle = "Important Fading Node" };
        var secFoundational = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = doc.StructuredLearningDocumentId, SubchapterTitle = "Foundational Node" };
        var secUncompleted = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = doc.StructuredLearningDocumentId, SubchapterTitle = "Uncompleted Section" };
        dbContext.StructuredLearningDocumentSections.AddRange(secCriticalFading, secCriticalFresh, secImportantFading, secFoundational, secUncompleted);

        // Fading completed 60 days ago (with S=8, recall is around 54% < 60%)
        var oldDate = DateTime.UtcNow.AddDays(-60);
        var freshDate = DateTime.UtcNow.AddHours(-2);

        dbContext.StructuredLearningAssessmentRounds.AddRange(
            new StructuredLearningAssessmentRound { StructuredLearningAssessmentRoundId = Guid.NewGuid(), PersonId = personId, StructuredLearningDocumentSectionId = secCriticalFading.StructuredLearningDocumentSectionId, Status = StructuredLearningAssessmentRoundStatus.Passed, CompletedDate = oldDate },
            new StructuredLearningAssessmentRound { StructuredLearningAssessmentRoundId = Guid.NewGuid(), PersonId = personId, StructuredLearningDocumentSectionId = secCriticalFresh.StructuredLearningDocumentSectionId, Status = StructuredLearningAssessmentRoundStatus.Passed, CompletedDate = freshDate },
            new StructuredLearningAssessmentRound { StructuredLearningAssessmentRoundId = Guid.NewGuid(), PersonId = personId, StructuredLearningDocumentSectionId = secImportantFading.StructuredLearningDocumentSectionId, Status = StructuredLearningAssessmentRoundStatus.Passed, CompletedDate = oldDate },
            new StructuredLearningAssessmentRound { StructuredLearningAssessmentRoundId = Guid.NewGuid(), PersonId = personId, StructuredLearningDocumentSectionId = secFoundational.StructuredLearningDocumentSectionId, Status = StructuredLearningAssessmentRoundStatus.Passed, CompletedDate = oldDate }
        );

        // Add impact scores
        dbContext.EngramImpactScores.AddRange(
            new EngramImpactScore { Id = Guid.NewGuid(), EmployeeId = personId, CourseId = doc.StructuredLearningDocumentId, EngramId = secCriticalFading.StructuredLearningDocumentSectionId.ToString(), Score = 90, Tier = "Critical" },
            new EngramImpactScore { Id = Guid.NewGuid(), EmployeeId = personId, CourseId = doc.StructuredLearningDocumentId, EngramId = secCriticalFresh.StructuredLearningDocumentSectionId.ToString(), Score = 90, Tier = "Critical" },
            new EngramImpactScore { Id = Guid.NewGuid(), EmployeeId = personId, CourseId = doc.StructuredLearningDocumentId, EngramId = secImportantFading.StructuredLearningDocumentSectionId.ToString(), Score = 60, Tier = "Important" },
            new EngramImpactScore { Id = Guid.NewGuid(), EmployeeId = personId, CourseId = doc.StructuredLearningDocumentId, EngramId = secFoundational.StructuredLearningDocumentSectionId.ToString(), Score = 40, Tier = "Foundational" }
        );

        await dbContext.SaveChangesAsync();

        var badgeService = new BadgeService(dbContext);
        var reviewService = new ReviewQueueService(dbContext, badgeService, NullLogger<ReviewQueueService>.Instance);

        var result = await reviewService.GetReviewQueueAsync(personId);

        result.Phase.Should().Be("Phase1");
        result.TotalPending.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].EngramId.Should().Be(secCriticalFading.StructuredLearningDocumentSectionId.ToString());
        result.Items[0].Tier.Should().Be("Critical");
        result.Items[0].RecallPercent.Should().BeLessThan(60);
        result.Items[0].PriorityScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetReviewQueueAsync_AppliesInterleaving_AlternatesAcrossDifferentCourses()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);

        // Setup Person with JobRole
        dbContext.People.Add(new Person
        {
            PersonId = personId,
            JobRoleId = roleId,
            AzureOid = "oid-1",
            AzureTid = "tid-1",
            Email = "student@example.com"
        });

        var skillA = new Skill { SkillId = Guid.NewGuid(), Name = "Course Alpha", Category = "job-skills", TenantId = Guid.NewGuid() };
        var skillB = new Skill { SkillId = Guid.NewGuid(), Name = "Course Beta", Category = "job-skills", TenantId = Guid.NewGuid() };
        dbContext.Skills.AddRange(skillA, skillB);
        dbContext.JobRoleSkills.AddRange(
            new JobRoleSkill { JobRoleId = roleId, SkillId = skillA.SkillId },
            new JobRoleSkill { JobRoleId = roleId, SkillId = skillB.SkillId }
        );

        var docA = new StructuredLearningDocument { StructuredLearningDocumentId = Guid.NewGuid(), DocumentName = "Course Alpha", TargetSkill = "Course Alpha" };
        var docB = new StructuredLearningDocument { StructuredLearningDocumentId = Guid.NewGuid(), DocumentName = "Course Beta", TargetSkill = "Course Beta" };
        dbContext.StructuredLearningDocuments.AddRange(docA, docB);

        // Course A has 2 fading critical sections
        var a1 = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = docA.StructuredLearningDocumentId, SubchapterTitle = "Alpha Engram 1" };
        var a2 = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = docA.StructuredLearningDocumentId, SubchapterTitle = "Alpha Engram 2" };

        // Course B has 2 fading critical sections
        var b1 = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = docB.StructuredLearningDocumentId, SubchapterTitle = "Beta Engram 1" };
        var b2 = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = docB.StructuredLearningDocumentId, SubchapterTitle = "Beta Engram 2" };
        dbContext.StructuredLearningDocumentSections.AddRange(a1, a2, b1, b2);

        var oldDate = DateTime.UtcNow.AddDays(-65);

        dbContext.StructuredLearningAssessmentRounds.AddRange(
            new StructuredLearningAssessmentRound { StructuredLearningAssessmentRoundId = Guid.NewGuid(), PersonId = personId, StructuredLearningDocumentSectionId = a1.StructuredLearningDocumentSectionId, Status = StructuredLearningAssessmentRoundStatus.Passed, CompletedDate = oldDate },
            new StructuredLearningAssessmentRound { StructuredLearningAssessmentRoundId = Guid.NewGuid(), PersonId = personId, StructuredLearningDocumentSectionId = a2.StructuredLearningDocumentSectionId, Status = StructuredLearningAssessmentRoundStatus.Passed, CompletedDate = oldDate },
            new StructuredLearningAssessmentRound { StructuredLearningAssessmentRoundId = Guid.NewGuid(), PersonId = personId, StructuredLearningDocumentSectionId = b1.StructuredLearningDocumentSectionId, Status = StructuredLearningAssessmentRoundStatus.Passed, CompletedDate = oldDate },
            new StructuredLearningAssessmentRound { StructuredLearningAssessmentRoundId = Guid.NewGuid(), PersonId = personId, StructuredLearningDocumentSectionId = b2.StructuredLearningDocumentSectionId, Status = StructuredLearningAssessmentRoundStatus.Passed, CompletedDate = oldDate }
        );

        dbContext.EngramImpactScores.AddRange(
            new EngramImpactScore { Id = Guid.NewGuid(), EmployeeId = personId, CourseId = docA.StructuredLearningDocumentId, EngramId = a1.StructuredLearningDocumentSectionId.ToString(), Score = 95, Tier = "Critical" },
            new EngramImpactScore { Id = Guid.NewGuid(), EmployeeId = personId, CourseId = docA.StructuredLearningDocumentId, EngramId = a2.StructuredLearningDocumentSectionId.ToString(), Score = 85, Tier = "Critical" },
            new EngramImpactScore { Id = Guid.NewGuid(), EmployeeId = personId, CourseId = docB.StructuredLearningDocumentId, EngramId = b1.StructuredLearningDocumentSectionId.ToString(), Score = 90, Tier = "Critical" },
            new EngramImpactScore { Id = Guid.NewGuid(), EmployeeId = personId, CourseId = docB.StructuredLearningDocumentId, EngramId = b2.StructuredLearningDocumentSectionId.ToString(), Score = 80, Tier = "Critical" }
        );

        await dbContext.SaveChangesAsync();

        var badgeService = new BadgeService(dbContext);
        var reviewService = new ReviewQueueService(dbContext, badgeService, NullLogger<ReviewQueueService>.Instance);

        var result = await reviewService.GetReviewQueueAsync(personId);

        result.TotalPending.Should().Be(4);
        result.Items.Should().HaveCount(4);

        // Interleaving check: Item 0 = Course A, Item 1 = Course B, Item 2 = Course A, Item 3 = Course B
        result.Items[0].CourseId.Should().Be(docA.StructuredLearningDocumentId); // A1 (Score 95)
        result.Items[1].CourseId.Should().Be(docB.StructuredLearningDocumentId); // B1 (Score 90)
        result.Items[2].CourseId.Should().Be(docA.StructuredLearningDocumentId); // A2 (Score 85)
        result.Items[3].CourseId.Should().Be(docB.StructuredLearningDocumentId); // B2 (Score 80)
    }

    [Fact]
    public async Task GetReviewQueueAsync_Phase2_IncludesBothCriticalAndImportantFading()
    {
        var options = CreateInMemoryOptions();
        var personId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        await using var dbContext = new HumanOsDbContext(options);

        // Setup Person with JobRole
        dbContext.People.Add(new Person
        {
            PersonId = personId,
            JobRoleId = roleId,
            AzureOid = "oid-1",
            AzureTid = "tid-1",
            Email = "student@example.com"
        });

        var skill = new Skill { SkillId = Guid.NewGuid(), Name = "API Mastery", Category = "job-skills", TenantId = Guid.NewGuid() };
        dbContext.Skills.Add(skill);
        dbContext.JobRoleSkills.Add(new JobRoleSkill { JobRoleId = roleId, SkillId = skill.SkillId });

        var doc = new StructuredLearningDocument { StructuredLearningDocumentId = Guid.NewGuid(), DocumentName = "API Mastery", TargetSkill = "API Mastery" };
        dbContext.StructuredLearningDocuments.Add(doc);

        var sec1 = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = doc.StructuredLearningDocumentId, SubchapterTitle = "Critical Fading" };
        var sec2 = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = doc.StructuredLearningDocumentId, SubchapterTitle = "Important Fading" };
        var sec3 = new StructuredLearningDocumentSection { StructuredLearningDocumentSectionId = Guid.NewGuid(), StructuredLearningDocumentId = doc.StructuredLearningDocumentId, SubchapterTitle = "Foundational Fading" };
        dbContext.StructuredLearningDocumentSections.AddRange(sec1, sec2, sec3);

        // Complete 100% of sections (1/1 ready courses = 100% -> Phase 2)
        var oldDate = DateTime.UtcNow.AddDays(-60); // Recall is ~54%
        dbContext.StructuredLearningAssessmentRounds.AddRange(
            new StructuredLearningAssessmentRound { StructuredLearningAssessmentRoundId = Guid.NewGuid(), PersonId = personId, StructuredLearningDocumentSectionId = sec1.StructuredLearningDocumentSectionId, Status = StructuredLearningAssessmentRoundStatus.Passed, CompletedDate = oldDate },
            new StructuredLearningAssessmentRound { StructuredLearningAssessmentRoundId = Guid.NewGuid(), PersonId = personId, StructuredLearningDocumentSectionId = sec2.StructuredLearningDocumentSectionId, Status = StructuredLearningAssessmentRoundStatus.Passed, CompletedDate = oldDate },
            new StructuredLearningAssessmentRound { StructuredLearningAssessmentRoundId = Guid.NewGuid(), PersonId = personId, StructuredLearningDocumentSectionId = sec3.StructuredLearningDocumentSectionId, Status = StructuredLearningAssessmentRoundStatus.Passed, CompletedDate = oldDate }
        );

        dbContext.EngramImpactScores.AddRange(
            new EngramImpactScore { Id = Guid.NewGuid(), EmployeeId = personId, CourseId = doc.StructuredLearningDocumentId, EngramId = sec1.StructuredLearningDocumentSectionId.ToString(), Score = 90, Tier = "Critical" },
            new EngramImpactScore { Id = Guid.NewGuid(), EmployeeId = personId, CourseId = doc.StructuredLearningDocumentId, EngramId = sec2.StructuredLearningDocumentSectionId.ToString(), Score = 65, Tier = "Important" },
            new EngramImpactScore { Id = Guid.NewGuid(), EmployeeId = personId, CourseId = doc.StructuredLearningDocumentId, EngramId = sec3.StructuredLearningDocumentSectionId.ToString(), Score = 40, Tier = "Foundational" }
        );

        await dbContext.SaveChangesAsync();

        var badgeService = new BadgeService(dbContext);
        var reviewService = new ReviewQueueService(dbContext, badgeService, NullLogger<ReviewQueueService>.Instance);

        var result = await reviewService.GetReviewQueueAsync(personId);

        result.Phase.Should().Be("Phase2");
        result.PhaseName.Should().Be("Maintenance");
        result.ReadyCoursesCompletionPct.Should().Be(100.0);
        result.TotalPending.Should().Be(2); // Critical + Important (Foundational skipped)
        result.Items.Should().Contain(x => x.Tier == "Critical");
        result.Items.Should().Contain(x => x.Tier == "Important");
        result.Items.Should().NotContain(x => x.Tier == "Foundational");
    }
}
