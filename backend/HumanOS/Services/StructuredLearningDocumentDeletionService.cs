using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Deletes a Structured Learning document and all content generated under it.
/// The document itself has cascade relationships to entities/connections, and
/// every section has cascade relationships to its steps, assessment rounds,
/// recall attempts, and question attempts. The only orphaned rows are the
/// section illustration metadata rows, which are removed explicitly here.
/// </summary>
public sealed class StructuredLearningDocumentDeletionService
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public StructuredLearningDocumentDeletionService(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var document = await dbContext.StructuredLearningDocuments
            .Include(d => d.Sections)
            .FirstOrDefaultAsync(d => d.StructuredLearningDocumentId == documentId, cancellationToken);

        if (document is null)
        {
            return false;
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var sectionIds = document.Sections
                .Select(s => s.StructuredLearningDocumentSectionId)
                .ToList();

            if (sectionIds.Count > 0)
            {
                var stepRows = await dbContext.StructuredLearningSectionSteps
                    .Where(s => sectionIds.Contains(s.StructuredLearningDocumentSectionId))
                    .ToListAsync(cancellationToken);

                if (stepRows.Count > 0)
                {
                    dbContext.StructuredLearningSectionSteps.RemoveRange(stepRows);
                }

                var assessmentRoundIds = await dbContext.StructuredLearningAssessmentRounds
                    .Where(r => sectionIds.Contains(r.StructuredLearningDocumentSectionId))
                    .Select(r => r.StructuredLearningAssessmentRoundId)
                    .ToListAsync(cancellationToken);

                if (assessmentRoundIds.Count > 0)
                {
                    var questionAttempts = await dbContext.StructuredLearningAssessmentQuestionAttempts
                        .Where(q => assessmentRoundIds.Contains(q.StructuredLearningAssessmentRoundId))
                        .ToListAsync(cancellationToken);

                    if (questionAttempts.Count > 0)
                    {
                        dbContext.StructuredLearningAssessmentQuestionAttempts.RemoveRange(questionAttempts);
                    }

                    var rounds = await dbContext.StructuredLearningAssessmentRounds
                        .Where(r => assessmentRoundIds.Contains(r.StructuredLearningAssessmentRoundId))
                        .ToListAsync(cancellationToken);

                    if (rounds.Count > 0)
                    {
                        dbContext.StructuredLearningAssessmentRounds.RemoveRange(rounds);
                    }
                }

                var recallAttempts = await dbContext.StructuredLearningRecallAttempts
                    .Where(r => sectionIds.Contains(r.StructuredLearningDocumentSectionId))
                    .ToListAsync(cancellationToken);

                if (recallAttempts.Count > 0)
                {
                    dbContext.StructuredLearningRecallAttempts.RemoveRange(recallAttempts);
                }

                var illustrations = await dbContext.StructuredLearningIllustrations
                    .Where(i => sectionIds.Contains(i.StructuredLearningDocumentSectionId))
                    .ToListAsync(cancellationToken);

                if (illustrations.Count > 0)
                {
                    dbContext.StructuredLearningIllustrations.RemoveRange(illustrations);
                }

                var sections = await dbContext.StructuredLearningDocumentSections
                    .Where(s => s.StructuredLearningDocumentId == documentId)
                    .ToListAsync(cancellationToken);

                if (sections.Count > 0)
                {
                    dbContext.StructuredLearningDocumentSections.RemoveRange(sections);
                }
            }

            dbContext.StructuredLearningDocuments.Remove(document);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        return true;
    }
}
