using FluentAssertions;
using HumanOS.Agents.Studio;
using HumanOS.Contracts.EmployeeScoring;
using HumanOS.Contracts.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Models.Learning;
using HumanOS.Models.People;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HumanOS.Tests.Services;

public sealed class StructuredLearningProfessorLectureServiceTests
{
    private static DbContextOptions<HumanOsDbContext> CreateInMemoryOptions()
    {
        return new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    [Fact]
    public void BuildPrompt_FormatsEngramUniversalContext_Correctly()
    {
        // Arrange
        var context = new StructuredLearningProfessorLectureContext
        {
            CourseTitle = "Introduction to Artificial Intelligence",
            ChapterTitle = "Foundations",
            SubchapterTitle = "Glossary of Core AI Concepts",
            Capability = "Define and distinguish AI, ML, Deep Learning and Generative AI",
            TeachingContent = "Artificial intelligence is becoming part of everyday work. Algorithms process data to generate outputs.",
            IllustrationDescription = "Whiteboard showing key AI terms top-to-bottom matching the lecture order.",
            DomainOrTopic = "Technology & AI",
            TargetLanguage = "en"
        };

        // Act
        var prompt = StructuredLearningProfessorLectureAgent.BuildPrompt(context);

        // Assert
        prompt.Should().Contain("Introduction to Artificial Intelligence");
        prompt.Should().Contain("Glossary of Core AI Concepts");
        prompt.Should().Contain("Artificial intelligence is becoming part of everyday work");
        prompt.Should().Contain("Whiteboard showing key AI terms top-to-bottom");
        prompt.Should().Contain("UNIVERSAL AUDIENCE ADDRESS");
        prompt.Should().NotContain("Elena Vance");
        prompt.Should().Contain("English (en)");
    }

    [Fact]
    public void BuildPrompt_SpanishLanguageDirective_SetProperly()
    {
        var context = new StructuredLearningProfessorLectureContext
        {
            SubchapterTitle = "Glosario de Términos",
            TeachingContent = "La inteligencia artificial forma parte de las actividades cotidianas.",
            TargetLanguage = "es"
        };

        var prompt = StructuredLearningProfessorLectureAgent.BuildPrompt(context);

        prompt.Should().Contain("Spanish (es)");
        prompt.Should().Contain("Glosario de Términos");
        prompt.Should().Contain("UNIVERSAL AUDIENCE ADDRESS");
    }

    [Fact]
    public async Task GenerateLectureForSectionAsync_ThrowsKeyNotFoundException_WhenSectionMissing()
    {
        var options = CreateInMemoryOptions();
        await using var dbContext = new HumanOsDbContext(options);

        var profileService = new EmployeeScoringProfileService(dbContext);
        var unconfiguredAgent = new StructuredLearningProfessorLectureAgent(new ConfigurationBuilder().Build());
        var emptyConfig = new ConfigurationBuilder().Build();
        var speechService = new AzureSpeechService(emptyConfig);
        var storageService = new Storage.CapabilityGraphIllustrationStorageService(emptyConfig);
        var service = new StructuredLearningProfessorLectureService(
            dbContext,
            profileService,
            unconfiguredAgent,
            speechService,
            storageService,
            NullLogger<StructuredLearningProfessorLectureService>.Instance);

        var act = () => service.GenerateLectureForSectionAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GenerateLectureForSectionAsync_BuildsContextAndThrowsIfAgentNotConfigured()
    {
        var options = CreateInMemoryOptions();
        var documentId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();

        await using (var dbContext = new HumanOsDbContext(options))
        {
            var document = new StructuredLearningDocument
            {
                StructuredLearningDocumentId = documentId,
                DocumentName = "Enterprise AI Guide",
                SessionId = Guid.NewGuid(),
                ConfigSnapshotJson = "{\"LanguageCode\":\"en\"}"
            };
            dbContext.StructuredLearningDocuments.Add(document);

            var section = new StructuredLearningDocumentSection
            {
                StructuredLearningDocumentSectionId = sectionId,
                StructuredLearningDocumentId = documentId,
                ChapterTitle = "Module 1",
                SubchapterTitle = "AI Core Concepts",
                Capability = "Understand core terminology",
                Text = "Raw section text about neural networks and tokens.",
                SortOrder = 1
            };
            dbContext.StructuredLearningDocumentSections.Add(section);

            // Add Teaching Step
            var teachingStep = new StructuredLearningSectionStep
            {
                StructuredLearningSectionStepId = Guid.NewGuid(),
                StructuredLearningDocumentSectionId = sectionId,
                StepType = StructuredLearningStepType.Teaching,
                Content = "Teaching step content: An algorithm is a sequence of processing steps.",
                SortOrder = 2
            };
            dbContext.StructuredLearningSectionSteps.Add(teachingStep);

            // Add Teaching Illustration
            var illustration = new StructuredLearningIllustration
            {
                StructuredLearningIllustrationId = Guid.NewGuid(),
                StructuredLearningDocumentSectionId = sectionId,
                Purpose = StructuredLearningIllustrationPurpose.Teaching,
                Caption = "Whiteboard diagram depicting algorithm steps",
                Prompt = "A clean whiteboard with algorithmic steps",
                StoragePath = "illustrations/test.png"
            };
            dbContext.StructuredLearningIllustrations.Add(illustration);

            // Add Student Person and Profile
            var person = new Person
            {
                PersonId = employeeId,
                Email = "student@humanos.ai",
                AzureOid = Guid.NewGuid().ToString(),
                AzureTid = Guid.NewGuid().ToString()
            };
            dbContext.People.Add(person);

            var personProfile = new PersonProfile
            {
                PersonProfileId = Guid.NewGuid(),
                PersonId = employeeId,
                FirstName = "Maria",
                LastName = "Gomez",
                DisplayName = "Maria Gomez",
                Occupation = "Lead Data Analyst"
            };
            dbContext.PersonProfiles.Add(personProfile);

            var resume = new PersonResume
            {
                PersonResumeId = Guid.NewGuid(),
                PersonId = employeeId,
                ProfessionalSummary = "Specialist in predictive modeling and business intelligence.",
                SkillsJson = "[\"Python\", \"SQL\", \"Power BI\", \"Statistical Analysis\"]"
            };
            dbContext.PersonResumes.Add(resume);

            // Add Impact Score
            dbContext.EngramImpactScores.Add(new EngramImpactScore
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                CourseId = documentId,
                EngramId = sectionId.ToString(),
                Score = 92,
                Tier = "Critical",
                Reason = "Fundamental for predictive modeling workflows.",
                GeneratedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
        }

        await using (var dbContext = new HumanOsDbContext(options))
        {
            var profileService = new EmployeeScoringProfileService(dbContext);
            var unconfiguredAgent = new StructuredLearningProfessorLectureAgent(new ConfigurationBuilder().Build());
            var emptyConfig = new ConfigurationBuilder().Build();
            var speechService = new AzureSpeechService(emptyConfig);
            var storageService = new Storage.CapabilityGraphIllustrationStorageService(emptyConfig);
            var service = new StructuredLearningProfessorLectureService(
                dbContext,
                profileService,
                unconfiguredAgent,
                speechService,
                storageService,
                NullLogger<StructuredLearningProfessorLectureService>.Instance);

            // Calling with unconfigured agent should throw InvalidOperationException from agent
            var act = () => service.GenerateLectureForSectionAsync(sectionId, employeeId);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Structured Learning Professor Lecture Agent is not configured*");
        }
    }
}
