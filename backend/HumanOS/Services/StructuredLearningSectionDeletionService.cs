using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Structured Learning's "Edit mode" delete-section feature (2026-08-11) —
/// mirrors Graph's CapabilityGraphPersistenceService.DeleteNodeAsync:
/// permanently removes ONE section and everything under it.
///
/// Cascade notes (unlike Graph's node, which has a real DB cascade FK for
/// its illustrations): StructuredLearningSectionStep, StructuredLearningRecallAttempt,
/// and StructuredLearningAssessmentRound (-> StructuredLearningAssessmentQuestionAttempt)
/// all have an ON DELETE CASCADE FK to StructuredLearningDocumentSection, so
/// deleting the section row cascades those automatically. StructuredLearningIllustration
/// has NO FK constraint at all (see StructuredLearningIllustrationConfiguration) —
/// its rows must be deleted explicitly here or they'd be orphaned.
///
/// After deleting, every LATER section in the same document has its
/// SortOrder shifted back by one, mirroring StructuredLearningSectionAdditionService's
/// own +1 shift on insert (keeps the document's chapter/section numbering
/// contiguous).
/// </summary>
public sealed class StructuredLearningSectionDeletionService
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public StructuredLearningSectionDeletionService(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>Returns false if no section with that id exists under the
    /// given document.</summary>
    public async Task<bool> DeleteSectionAsync(Guid documentId, Guid sectionId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var section = await dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.StructuredLearningDocumentSectionId == sectionId && s.StructuredLearningDocumentId == documentId,
                cancellationToken);

        if (section is null)
        {
            return false;
        }

        // SqlServerRetryingExecutionStrategy (EnableRetryOnFailure) refuses a
        // user-managed transaction directly — the strategy itself must own
        // and retry the whole atomic unit via ExecuteAsync (same pattern as
        // CapabilityGraphPersistenceService.DeleteNodeAsync).
        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            await dbContext.StructuredLearningIllustrations
                .Where(i => i.StructuredLearningDocumentSectionId == sectionId)
                .ExecuteDeleteAsync(cancellationToken);

            await dbContext.StructuredLearningDocumentSections
                .Where(s => s.StructuredLearningDocumentSectionId == sectionId)
                .ExecuteDeleteAsync(cancellationToken);

            await dbContext.StructuredLearningDocumentSections
                .Where(s => s.StructuredLearningDocumentId == documentId && s.SortOrder > section.SortOrder)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SortOrder, x => x.SortOrder - 1), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });

        return true;
    }
}
