using HumanOS.Contracts.Programs;
using HumanOS.Data;
using HumanOS.Models.Programs;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// "Inscribirme" (2026-07-25) — explicit enrollment in a Program directly
/// from a browsable catalog (/programs/catalog on the frontend), distinct
/// from the pre-existing Growth Plan Starting Point recommendation-
/// acceptance path. Mirrors CapabilityEnrollmentService's role for
/// standalone Capabilities. Both enrollment paths are merged together by
/// GrowthPlanService.GetEnrolledProgramsAsync so "Mis Programas" always
/// shows a single unified list regardless of how the person got enrolled.
/// </summary>
public sealed class ProgramEnrollmentService
{
    private readonly HumanOsDbContext _dbContext;

    public ProgramEnrollmentService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Idempotent — enrolling twice in the same Program just
    /// returns the existing enrollment untouched (its original
    /// EnrolledDate is preserved).</summary>
    public async Task<ProgramEnrollmentActionResponse> EnrollAsync(
        Guid personId,
        Guid programId,
        CancellationToken cancellationToken = default)
    {
        var programExists = await _dbContext.Programs
            .AsNoTracking()
            .AnyAsync(p => p.ProgramId == programId, cancellationToken);
        if (!programExists)
        {
            throw new InvalidOperationException($"Program {programId} not found.");
        }

        var existing = await _dbContext.ProgramEnrollments
            .FirstOrDefaultAsync(x => x.PersonId == personId && x.ProgramId == programId, cancellationToken);

        if (existing is null)
        {
            existing = new ProgramEnrollment
            {
                ProgramEnrollmentId = Guid.NewGuid(),
                PersonId = personId,
                ProgramId = programId,
                EnrolledDate = DateTime.UtcNow
            };
            _dbContext.ProgramEnrollments.Add(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ProgramEnrollmentActionResponse
        {
            ProgramId = programId,
            Enrolled = true,
            EnrolledDate = existing.EnrolledDate
        };
    }

    /// <summary>"Quitar" — removes the direct enrollment record. Never
    /// touches LearningSession/progress data. NOTE: if this Program was
    /// ALSO accepted as a Growth Plan recommendation, it will still appear
    /// in "Mis Programas" via that path — this only removes the CATALOG
    /// enrollment record.</summary>
    public async Task<ProgramEnrollmentActionResponse> UnenrollAsync(
        Guid personId,
        Guid programId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.ProgramEnrollments
            .FirstOrDefaultAsync(x => x.PersonId == personId && x.ProgramId == programId, cancellationToken);

        if (existing is not null)
        {
            _dbContext.ProgramEnrollments.Remove(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ProgramEnrollmentActionResponse
        {
            ProgramId = programId,
            Enrolled = false,
            EnrolledDate = null
        };
    }
}
