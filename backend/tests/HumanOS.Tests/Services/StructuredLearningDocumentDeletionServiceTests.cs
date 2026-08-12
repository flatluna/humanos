using FluentAssertions;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HumanOS.Tests.Services;

public class StructuredLearningDocumentDeletionServiceTests
{
    [Fact]
    public async Task DeleteDocumentAsync_DeletesDocumentAndAllDependentRows()
    {
        var options = new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using (var ctx = new HumanOsDbContext(options))
        {
            var document = new StructuredLearningDocument
            {
                StructuredLearningDocumentId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                DocumentName = "Test document",
                CreatedDate = DateTime.UtcNow,
            };

            var section = new StructuredLearningDocumentSection
            {
                StructuredLearningDocumentSectionId = Guid.NewGuid(),
                StructuredLearningDocumentId = document.StructuredLearningDocumentId,
                TenantId = document.TenantId,
                SortOrder = 1,
                ChapterTitle = "Chapter 1",
                SubchapterTitle = "Intro",
                Text = "Section text",
                TotalCharacters = 12,
                TotalTokens = 2,
                Embedding = new Microsoft.Data.SqlTypes.SqlVector<float>(new[] { 1f, 2f, 3f }),
                CreatedDate = DateTime.UtcNow,
                Document = document,
            };

            var step = new StructuredLearningSectionStep
            {
                StructuredLearningSectionStepId = Guid.NewGuid(),
                StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                TenantId = document.TenantId,
                StepType = StructuredLearningStepType.Hypothesis,
                SortOrder = 1,
                Content = "Some content",
                CurrentDeepDiveCardIndex = 0,
                IsHumanApproved = true,
                CreatedDate = DateTime.UtcNow,
                Section = section,
            };

            var illustration = new StructuredLearningIllustration
            {
                StructuredLearningIllustrationId = Guid.NewGuid(),
                StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                StoragePath = "illustrations/test.png",
                Prompt = "Prompt",
                ImageModel = "gpt-image-1",
                Caption = "caption",
                Purpose = StructuredLearningIllustrationPurpose.DeepDiveCard,
                Width = 512,
                Height = 512,
                CreatedDate = DateTime.UtcNow,
            };

            var round = new StructuredLearningAssessmentRound
            {
                StructuredLearningAssessmentRoundId = Guid.NewGuid(),
                StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                RoundNumber = 1,
                Status = StructuredLearningAssessmentRoundStatus.InProgress,
                CreatedDate = DateTime.UtcNow,
                Section = section,
            };

            var questionAttempt = new StructuredLearningAssessmentQuestionAttempt
            {
                StructuredLearningAssessmentQuestionAttemptId = Guid.NewGuid(),
                StructuredLearningAssessmentRoundId = round.StructuredLearningAssessmentRoundId,
                QuestionIndex = 1,
                QuestionText = "Question?",
                OptionsJson = "[]",
                Correctness = StructuredLearningAssessmentAnswerCorrectness.Pending,
                CreatedDate = DateTime.UtcNow,
                Round = round,
            };

            var recallAttempt = new StructuredLearningRecallAttempt
            {
                StructuredLearningRecallAttemptId = Guid.NewGuid(),
                StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                TutorPromptShown = "Prompt",
                StudentResponse = "Answer",
                RecallScore = 100,
                CreatedDate = DateTime.UtcNow,
                Section = section,
            };

            document.Sections = new List<StructuredLearningDocumentSection> { section };
            section.Steps = new List<StructuredLearningSectionStep> { step };

            ctx.StructuredLearningDocuments.Add(document);
            ctx.StructuredLearningDocumentSections.Add(section);
            ctx.StructuredLearningSectionSteps.Add(step);
            ctx.StructuredLearningIllustrations.Add(illustration);
            ctx.StructuredLearningAssessmentRounds.Add(round);
            ctx.StructuredLearningAssessmentQuestionAttempts.Add(questionAttempt);
            ctx.StructuredLearningRecallAttempts.Add(recallAttempt);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new HumanOsDbContext(options))
        {
            var service = new StructuredLearningDocumentDeletionService(new FixedDbContextFactory(ctx));
            var documentId = await ctx.StructuredLearningDocuments.Select(d => d.StructuredLearningDocumentId).SingleAsync();
            var deleted = await service.DeleteDocumentAsync(documentId, CancellationToken.None);

            deleted.Should().BeTrue();
            ctx.StructuredLearningDocuments.Should().BeEmpty();
            ctx.StructuredLearningDocumentSections.Should().BeEmpty();
            ctx.StructuredLearningSectionSteps.Should().BeEmpty();
            ctx.StructuredLearningIllustrations.Should().BeEmpty();
            ctx.StructuredLearningAssessmentRounds.Should().BeEmpty();
            ctx.StructuredLearningAssessmentQuestionAttempts.Should().BeEmpty();
            ctx.StructuredLearningRecallAttempts.Should().BeEmpty();
        }
    }

    private sealed class FixedDbContextFactory(HumanOsDbContext dbContext) : IDbContextFactory<HumanOsDbContext>
    {
        public HumanOsDbContext CreateDbContext() => dbContext;

        public ValueTask<HumanOsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => new(dbContext);
    }
}
