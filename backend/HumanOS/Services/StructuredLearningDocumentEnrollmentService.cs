using HumanOS.Contracts.Capabilities;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// "Inscribirme" for a Structured Learning document catalog card — mirrors
/// <see cref="CapabilityEnrollmentService"/>'s role for standalone
/// Capabilities. See <see cref="StructuredLearningDocumentEnrollment"/>.
/// </summary>
public sealed class StructuredLearningDocumentEnrollmentService
{
    private readonly HumanOsDbContext _dbContext;

    public StructuredLearningDocumentEnrollmentService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Idempotent — enrolling twice just returns the existing
    /// enrollment untouched (its original EnrolledDate is preserved).</summary>
    public async Task<StructuredLearningDocumentEnrollmentActionResponse> EnrollAsync(
        Guid personId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var documentExists = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .AnyAsync(d => d.StructuredLearningDocumentId == documentId, cancellationToken);
        if (!documentExists)
        {
            throw new InvalidOperationException($"Structured learning document {documentId} not found.");
        }

        var existing = await _dbContext.StructuredLearningDocumentEnrollments
            .FirstOrDefaultAsync(x => x.PersonId == personId && x.StructuredLearningDocumentId == documentId, cancellationToken);

        if (existing is null)
        {
            existing = new StructuredLearningDocumentEnrollment
            {
                StructuredLearningDocumentEnrollmentId = Guid.NewGuid(),
                PersonId = personId,
                StructuredLearningDocumentId = documentId,
                EnrolledDate = DateTime.UtcNow
            };
            _dbContext.StructuredLearningDocumentEnrollments.Add(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new StructuredLearningDocumentEnrollmentActionResponse
        {
            StructuredLearningDocumentId = documentId,
            Enrolled = true,
            EnrolledDate = existing.EnrolledDate
        };
    }

    public async Task<StructuredLearningDocumentEnrollmentActionResponse> UnenrollAsync(
        Guid personId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.StructuredLearningDocumentEnrollments
            .FirstOrDefaultAsync(x => x.PersonId == personId && x.StructuredLearningDocumentId == documentId, cancellationToken);

        if (existing is not null)
        {
            _dbContext.StructuredLearningDocumentEnrollments.Remove(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new StructuredLearningDocumentEnrollmentActionResponse
        {
            StructuredLearningDocumentId = documentId,
            Enrolled = false,
            EnrolledDate = null
        };
    }

    public async Task<IReadOnlyList<EnrolledStructuredLearningDocumentResponse>> GetEnrolledDocumentsAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var explicitEnrollments = await _dbContext.StructuredLearningDocumentEnrollments
            .AsNoTracking()
            .Where(x => x.PersonId == personId)
            .Select(x => new { DocumentId = x.StructuredLearningDocumentId, Date = x.EnrolledDate })
            .ToListAsync(cancellationToken);

        var roundDocIds = await _dbContext.StructuredLearningAssessmentRounds
            .AsNoTracking()
            .Where(r => r.PersonId == personId)
            .Join(
                _dbContext.StructuredLearningDocumentSections.AsNoTracking(),
                r => r.StructuredLearningDocumentSectionId,
                s => s.StructuredLearningDocumentSectionId,
                (r, s) => new { DocumentId = s.StructuredLearningDocumentId, Date = r.CompletedDate ?? r.CreatedDate })
            .ToListAsync(cancellationToken);

        var rewardDocIds = await _dbContext.RewardLedgerEntries
            .AsNoTracking()
            .Where(r => r.PersonId == personId && (r.SourceType == "Structured" || r.SourceType == null))
            .Select(r => new { DocumentId = r.CourseId, Date = r.EarnedAt })
            .ToListAsync(cancellationToken);

        var allDocItems = explicitEnrollments
            .Concat(roundDocIds)
            .Concat(rewardDocIds)
            .GroupBy(x => x.DocumentId)
            .Select(g => new { DocumentId = g.Key, EnrolledDate = g.Max(x => x.Date) })
            .ToList();

        var results = new List<EnrolledStructuredLearningDocumentResponse>();
        foreach (var item in allDocItems)
        {
            var document = await _dbContext.StructuredLearningDocuments
                .AsNoTracking()
                .Include(d => d.Sections)
                .FirstOrDefaultAsync(d => d.StructuredLearningDocumentId == item.DocumentId, cancellationToken);

            if (document is null)
            {
                continue;
            }

            var firstSectionId = document.Sections
                .OrderBy(s => s.SortOrder)
                .Select(s => s.StructuredLearningDocumentSectionId)
                .FirstOrDefault();

            var coverIllustrationId = firstSectionId == default
                ? null
                : await _dbContext.StructuredLearningIllustrations
                    .AsNoTracking()
                    .Where(i => i.StructuredLearningDocumentSectionId == firstSectionId)
                    .OrderBy(i => i.CreatedDate)
                    .Select(i => (Guid?)i.StructuredLearningIllustrationId)
                    .FirstOrDefaultAsync(cancellationToken);

            var sectionIds = document.Sections.Select(s => s.StructuredLearningDocumentSectionId).ToList();
            var completedSectionCount = await _dbContext.StructuredLearningAssessmentRounds
                .AsNoTracking()
                .Where(r => r.PersonId == personId && r.Status == HumanOS.Models.Capabilities.Studio.StructuredLearningAssessmentRoundStatus.Passed && sectionIds.Contains(r.StructuredLearningDocumentSectionId))
                .Select(r => r.StructuredLearningDocumentSectionId)
                .Distinct()
                .CountAsync(cancellationToken);

            results.Add(new EnrolledStructuredLearningDocumentResponse
            {
                StructuredLearningDocumentId = document.StructuredLearningDocumentId,
                DocumentName = document.DocumentName,
                SectionCount = document.Sections.Count,
                CompletedSectionCount = completedSectionCount,
                CoverIllustrationId = coverIllustrationId,
                EnrolledDate = item.EnrolledDate,
                Language = StructuredLearningLanguageHeuristic.Resolve(document.ConfigSnapshotJson, document.ExecutiveSummary ?? document.DocumentName),
            });
        }

        return results.OrderByDescending(r => r.EnrolledDate).ToList();
    }
}
