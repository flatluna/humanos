using HumanOS.Contracts.Capabilities;
using HumanOS.Data;
using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// "Mis Capacidades" (2026-07-24) — explicit enrollment ("Inscribirme") in a
/// standalone Capability, distinct from the always-free "Empezar"/run flow
/// on /capabilities. See <see cref="CapabilityEnrollment"/>'s doc comment
/// for the full rationale. Progress is always computed live via
/// <see cref="GraphProgressionEngine"/> — never stored on the enrollment
/// row itself, so there is a single source of truth for progress, same
/// pattern as GrowthPlanService.GetEnrolledProgramsAsync for "Mis Programas".
/// </summary>
public sealed class CapabilityEnrollmentService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly GraphProgressionEngine _graphProgressionEngine;

    public CapabilityEnrollmentService(HumanOsDbContext dbContext, GraphProgressionEngine graphProgressionEngine)
    {
        _dbContext = dbContext;
        _graphProgressionEngine = graphProgressionEngine;
    }

    /// <summary>Idempotent — enrolling twice in the same Capability just
    /// returns the existing enrollment untouched (its original
    /// EnrolledDate is preserved).</summary>
    public async Task<CapabilityEnrollmentActionResponse> EnrollAsync(
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken = default)
    {
        var capabilityExists = await _dbContext.Capabilities
            .AsNoTracking()
            .AnyAsync(c => c.CapabilityId == capabilityId, cancellationToken);
        if (!capabilityExists)
        {
            throw new InvalidOperationException($"Capability {capabilityId} not found.");
        }

        var existing = await _dbContext.CapabilityEnrollments
            .FirstOrDefaultAsync(x => x.PersonId == personId && x.CapabilityId == capabilityId, cancellationToken);

        if (existing is null)
        {
            existing = new CapabilityEnrollment
            {
                CapabilityEnrollmentId = Guid.NewGuid(),
                PersonId = personId,
                CapabilityId = capabilityId,
                EnrolledDate = DateTime.UtcNow
            };
            _dbContext.CapabilityEnrollments.Add(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new CapabilityEnrollmentActionResponse
        {
            CapabilityId = capabilityId,
            Enrolled = true,
            EnrolledDate = existing.EnrolledDate
        };
    }

    /// <summary>"Quitar" — removes the enrollment record. Never touches
    /// LearningSession/progress data, so unenrolling and re-enrolling later
    /// picks up progress exactly where it was left (progress lives on the
    /// graph, not on the enrollment row).</summary>
    public async Task<CapabilityEnrollmentActionResponse> UnenrollAsync(
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.CapabilityEnrollments
            .FirstOrDefaultAsync(x => x.PersonId == personId && x.CapabilityId == capabilityId, cancellationToken);

        if (existing is not null)
        {
            _dbContext.CapabilityEnrollments.Remove(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new CapabilityEnrollmentActionResponse
        {
            CapabilityId = capabilityId,
            Enrolled = false,
            EnrolledDate = null
        };
    }

    public async Task<IReadOnlyList<EnrolledCapabilityResponse>> GetEnrolledCapabilitiesAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var enrollments = await _dbContext.CapabilityEnrollments
            .AsNoTracking()
            .Where(x => x.PersonId == personId)
            .OrderByDescending(x => x.EnrolledDate)
            .ToListAsync(cancellationToken);

        var results = new List<EnrolledCapabilityResponse>();
        foreach (var enrollment in enrollments)
        {
            var response = await BuildResponseAsync(enrollment, cancellationToken);
            if (response is not null)
            {
                results.Add(response);
            }
        }

        return results;
    }

    private async Task<EnrolledCapabilityResponse?> BuildResponseAsync(
        CapabilityEnrollment enrollment,
        CancellationToken cancellationToken)
    {
        var capability = await _dbContext.Capabilities
            .AsNoTracking()
            .Include(c => c.Subject)
            .FirstOrDefaultAsync(c => c.CapabilityId == enrollment.CapabilityId, cancellationToken);

        if (capability is null)
        {
            return null;
        }

        var graph = await _graphProgressionEngine.GetFullGraphAsync(
            _dbContext, enrollment.PersonId, enrollment.CapabilityId, cancellationToken: cancellationToken);

        var totalNodes = graph?.Nodes.Count ?? 0;
        var masteredNodes = graph?.Nodes.Count(n => n.State == "Mastered") ?? 0;
        var scores = graph?.Nodes
            .Where(n => n.FinalScore.HasValue)
            .Select(n => n.FinalScore!.Value)
            .ToList() ?? [];

        var hasSession = totalNodes > 0 && await _dbContext.LearningSessions
            .AsNoTracking()
            .AnyAsync(s => s.PersonId == enrollment.PersonId
                        && s.CapabilityId == enrollment.CapabilityId
                        && s.ProgramId == null, cancellationToken);

        var status = totalNodes > 0 && masteredNodes == totalNodes
            ? "Completed"
            : masteredNodes > 0 || hasSession
                ? "InProgress"
                : "NotStarted";

        return new EnrolledCapabilityResponse
        {
            CapabilityId = capability.CapabilityId,
            Name = capability.Name,
            Description = capability.Description,
            SubjectCode = capability.Subject?.Code,
            SubjectName = capability.Subject?.Name,
            Category = capability.Category,
            EnrolledDate = enrollment.EnrolledDate,
            Status = status,
            TotalNodes = totalNodes,
            MasteredNodes = masteredNodes,
            AverageScore = scores.Count > 0 ? (int)Math.Round(scores.Average()) : null
        };
    }
}
