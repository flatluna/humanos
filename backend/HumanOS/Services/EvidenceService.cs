using HumanOS.Contracts.Responses;
using HumanOS.Data;
using HumanOS.Models.Capabilities;
using EvidenceModel = HumanOS.Models.Evidence.Evidence;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class EvidenceService
{
    private readonly HumanOsDbContext _dbContext;

    public EvidenceService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EvidenceResponse?> SubmitAsync(
        Guid personId,
        Guid capabilityId,
        Guid? personProjectId,
        string title,
        string? description,
        string evidenceType,
        string? evidenceUrl,
        int assistanceLevel,
        CancellationToken cancellationToken = default)
    {
        ValidateAssistanceLevel(assistanceLevel);

        var personExists = await _dbContext.People
            .AnyAsync(
                p => p.PersonId == personId && p.IsActive,
                cancellationToken);

        if (!personExists)
        {
            throw new KeyNotFoundException(
                "The requested person was not found or is not active.");
        }

        var capabilityExists = await _dbContext.Capabilities
            .AnyAsync(
                c => c.CapabilityId == capabilityId && c.IsActive,
                cancellationToken);

        if (!capabilityExists)
        {
            throw new KeyNotFoundException(
                "The requested capability was not found or is not active.");
        }

        var personCapability = await _dbContext.PersonCapabilities
            .SingleOrDefaultAsync(
                pc => pc.PersonId == personId && pc.CapabilityId == capabilityId,
                cancellationToken);

        if (personCapability is null)
        {
            throw new InvalidOperationException(
                "The person has not started developing this capability.");
        }

        var evidence = new EvidenceModel
        {
            EvidenceId = Guid.NewGuid(),
            PersonId = personId,
            CapabilityId = capabilityId,
            PersonProjectId = personProjectId,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            EvidenceType = evidenceType.Trim(),
            EvidenceUrl = string.IsNullOrWhiteSpace(evidenceUrl) ? null : evidenceUrl.Trim(),
            ValidationStatus = "Pending",
            AssistanceLevel = assistanceLevel,
            ValidationFeedback = null,
            ValidatedDate = null,
            SubmittedDate = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow
        };

        _dbContext.Evidence.Add(evidence);

        // Create CapabilityEvidence linking with explicit ID assignment
        var capabilityEvidence = new CapabilityEvidence
        {
            CapabilityEvidenceId = Guid.NewGuid(),  // Explicit assignment as per spec
            PersonCapabilityId = personCapability.PersonCapabilityId,
            EvidenceId = evidence.EvidenceId,
            EvidenceType = evidenceType.Trim(),
            ContributionWeight = 1.0m,
            ValidationStatus = "Pending",
            ValidatedByPersonId = null,
            ValidatedDate = null,
            CreatedDate = DateTime.UtcNow
        };

        _dbContext.CapabilityEvidence.Add(capabilityEvidence);

        // Update PersonCapability
        personCapability.LastActivityDate = DateTime.UtcNow;
        personCapability.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(evidence);
    }

    public async Task<EvidenceResponse?> GetByIdAsync(
        Guid evidenceId,
        CancellationToken cancellationToken = default)
    {
        var evidence = await _dbContext.Evidence
            .AsNoTracking()
            .SingleOrDefaultAsync(
                e => e.EvidenceId == evidenceId,
                cancellationToken);

        if (evidence is null)
        {
            return null;
        }

        return MapToResponse(evidence);
    }

    public async Task<IReadOnlyList<EvidenceResponse>> GetByPersonAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var evidence = await _dbContext.Evidence
            .AsNoTracking()
            .Where(e => e.PersonId == personId)
            .OrderByDescending(e => e.SubmittedDate)
            .ToListAsync(cancellationToken);

        return evidence.Select(MapToResponse).ToList();
    }

    public async Task<EvidenceResponse?> ValidateAsync(
        Guid evidenceId,
        string validationStatus,
        string? validationFeedback,
        CancellationToken cancellationToken = default)
    {
        ValidateStatus(validationStatus);

        var evidence = await _dbContext.Evidence
            .SingleOrDefaultAsync(
                e => e.EvidenceId == evidenceId,
                cancellationToken);

        if (evidence is null)
        {
            return null;
        }

        evidence.ValidationStatus = validationStatus;
        evidence.ValidationFeedback = string.IsNullOrWhiteSpace(validationFeedback) ? null : validationFeedback.Trim();
        evidence.ValidatedDate = DateTime.UtcNow;

        // Update associated CapabilityEvidence records
        var capabilityEvidenceRecords = await _dbContext.CapabilityEvidence
            .Where(ce => ce.EvidenceId == evidenceId)
            .ToListAsync(cancellationToken);

        foreach (var ce in capabilityEvidenceRecords)
        {
            ce.ValidationStatus = validationStatus;
            ce.ValidatedDate = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(evidence);
    }

    private static void ValidateAssistanceLevel(int assistanceLevel)
    {
        if (assistanceLevel < 0 || assistanceLevel > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(assistanceLevel),
                "Assistance level must be between 0 and 5.");
        }
    }

    private static void ValidateStatus(string status)
    {
        var validStatuses = new[] { "Pending", "Approved", "Rejected", "Archived" };
        if (!validStatuses.Contains(status))
        {
            throw new ArgumentException(
                $"Invalid validation status. Must be one of: {string.Join(", ", validStatuses)}",
                nameof(status));
        }
    }

    private static EvidenceResponse MapToResponse(EvidenceModel evidence)
    {
        return new EvidenceResponse
        {
            EvidenceId = evidence.EvidenceId,
            PersonId = evidence.PersonId,
            CapabilityId = evidence.CapabilityId,
            PersonProjectId = evidence.PersonProjectId,
            Title = evidence.Title,
            Description = evidence.Description,
            EvidenceType = evidence.EvidenceType,
            EvidenceUrl = evidence.EvidenceUrl,
            ValidationStatus = evidence.ValidationStatus,
            AssistanceLevel = evidence.AssistanceLevel,
            ValidationFeedback = evidence.ValidationFeedback,
            ValidatedDate = evidence.ValidatedDate,
            SubmittedDate = evidence.SubmittedDate,
            CreatedDate = evidence.CreatedDate
        };
    }
}
