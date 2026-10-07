using HumanOS.Contracts.Tenancy;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>Lists the courses a tenant actually CREATED (2026-10-01) — for
/// Capabilities, "owned by tenant" means <c>OwnerTenantId == tenantId</c>;
/// note that flipping a Capability to the global catalog
/// (<see cref="CapabilityService.UpdateVisibilityAsync"/>) CLEARS
/// OwnerTenantId, so a Capability can never appear here as both
/// tenant-owned AND global at the same time under the current data model
/// — <see cref="TenantCourseCardResponse.IsGlobal"/> is therefore always
/// false for Capability rows. Structured Learning Documents keep their
/// TenantId even after being marked global (<c>IsGlobal</c> is a separate
/// bool), so they CAN show IsGlobal = true here.</summary>
public sealed class TenantCoursesService
{
    private readonly HumanOsDbContext _dbContext;

    public TenantCoursesService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TenantCourseCardResponse>> GetTenantCoursesAsync(
        Guid tenantId, CancellationToken cancellationToken)
    {
        var capabilities = await _dbContext.Capabilities
            .Where(c => c.OwnerTenantId == tenantId)
            .Select(c => new TenantCourseCardResponse
            {
                CourseId = c.CapabilityId,
                Name = c.Name,
                CourseType = "Capability",
                IsGlobal = false,
                LanguageCode = c.ContentLanguageCode,
                CreatedDate = c.CreatedDate,
            })
            .ToListAsync(cancellationToken);

        var structuredLearningDocuments = await _dbContext.StructuredLearningDocuments
            .Where(d => d.TenantId == tenantId)
            .Select(d => new TenantCourseCardResponse
            {
                CourseId = d.StructuredLearningDocumentId,
                Name = d.DocumentName,
                CourseType = "StructuredLearning",
                IsGlobal = d.IsGlobal,
                LanguageCode = null,
                CreatedDate = d.CreatedDate,
            })
            .ToListAsync(cancellationToken);

        return capabilities
            .Concat(structuredLearningDocuments)
            .OrderByDescending(c => c.CreatedDate)
            .ToList();
    }
}
