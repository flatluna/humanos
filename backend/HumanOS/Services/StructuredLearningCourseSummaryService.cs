using HumanOS.Contracts.Studio;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>Computes the quick "engrams / chapters / facts" counts for a
/// Structured Learning Document's detail page (2026-10-01) — see
/// <see cref="StructuredLearningCourseSummaryResponse"/> doc comment.</summary>
public sealed class StructuredLearningCourseSummaryService
{
    private readonly HumanOsDbContext _dbContext;

    public StructuredLearningCourseSummaryService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<StructuredLearningCourseSummaryResponse?> GetSummaryAsync(
        Guid documentId, CancellationToken cancellationToken)
    {
        var documentExists = await _dbContext.StructuredLearningDocuments
            .AnyAsync(d => d.StructuredLearningDocumentId == documentId, cancellationToken);
        if (!documentExists)
        {
            return null;
        }

        var sectionIds = await _dbContext.StructuredLearningDocumentSections
            .Where(s => s.StructuredLearningDocumentId == documentId)
            .Select(s => s.StructuredLearningDocumentSectionId)
            .ToListAsync(cancellationToken);

        var chaptersCount = await _dbContext.StructuredLearningDocumentSections
            .Where(s => s.StructuredLearningDocumentId == documentId)
            .Select(s => s.ChapterTitle)
            .Distinct()
            .CountAsync(cancellationToken);

        var factsCount = await _dbContext.StructuredLearningKnowledgeFacts
            .CountAsync(f => sectionIds.Contains(f.EngramId), cancellationToken);

        return new StructuredLearningCourseSummaryResponse
        {
            StructuredLearningDocumentId = documentId,
            EngramsCount = sectionIds.Count,
            ChaptersCount = chaptersCount,
            FactsCount = factsCount,
        };
    }
}
