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
        var enrollments = await _dbContext.StructuredLearningDocumentEnrollments
            .AsNoTracking()
            .Where(x => x.PersonId == personId)
            .OrderByDescending(x => x.EnrolledDate)
            .ToListAsync(cancellationToken);

        var results = new List<EnrolledStructuredLearningDocumentResponse>();
        foreach (var enrollment in enrollments)
        {
            var document = await _dbContext.StructuredLearningDocuments
                .AsNoTracking()
                .Include(d => d.Sections)
                .FirstOrDefaultAsync(d => d.StructuredLearningDocumentId == enrollment.StructuredLearningDocumentId, cancellationToken);

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

            results.Add(new EnrolledStructuredLearningDocumentResponse
            {
                StructuredLearningDocumentId = document.StructuredLearningDocumentId,
                DocumentName = document.DocumentName,
                SectionCount = document.Sections.Count,
                CoverIllustrationId = coverIllustrationId,
                EnrolledDate = enrollment.EnrolledDate
            });
        }

        return results;
    }
}
