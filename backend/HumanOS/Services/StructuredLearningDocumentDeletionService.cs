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

        var documentExists = await dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .AnyAsync(d => d.StructuredLearningDocumentId == documentId, cancellationToken);

        if (!documentExists)
        {
            return false;
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var sectionIds = await dbContext.StructuredLearningDocumentSections
                .AsNoTracking()
                .Where(s => s.StructuredLearningDocumentId == documentId)
                .Select(s => s.StructuredLearningDocumentSectionId)
                .ToListAsync(cancellationToken);

            if (sectionIds.Count > 0)
            {
                await dbContext.StructuredLearningIllustrations
                    .Where(i => sectionIds.Contains(i.StructuredLearningDocumentSectionId))
                    .ExecuteDeleteAsync(cancellationToken);

                await dbContext.StructuredLearningDocumentSections
                    .Where(s => s.StructuredLearningDocumentId == documentId)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            await dbContext.StructuredLearningDocuments
                .Where(d => d.StructuredLearningDocumentId == documentId)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });

        return true;
    }
}
