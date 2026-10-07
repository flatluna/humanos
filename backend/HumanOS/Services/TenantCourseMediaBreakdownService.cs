using HumanOS.Contracts.Tenancy;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>Per-course image/video counts for a tenant's "Imágenes y
/// videos" histogram (2026-10-01) — same ownership scope as
/// <see cref="TenantCoursesService"/> (only courses this tenant itself
/// created), returns the top N courses by total media count.</summary>
public sealed class TenantCourseMediaBreakdownService
{
    private readonly HumanOsDbContext _dbContext;

    public TenantCourseMediaBreakdownService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TenantCourseMediaBreakdownResponse>> GetTopCoursesAsync(
        Guid tenantId, int top, CancellationToken cancellationToken)
    {
        var capabilityMedia = await _dbContext.Set<Models.Capabilities.Graph.CapabilityGraphNodeIllustration>()
            .Join(_dbContext.CapabilityGraphNodes, i => i.CapabilityGraphNodeId, n => n.CapabilityGraphNodeId, (i, n) => n.CapabilityGraphId)
            .Join(_dbContext.CapabilityGraphs, graphId => graphId, g => g.CapabilityGraphId, (graphId, g) => g.CapabilityId)
            .Join(_dbContext.Capabilities.Where(c => c.OwnerTenantId == tenantId), capabilityId => capabilityId, c => c.CapabilityId, (capabilityId, c) => c)
            .GroupBy(c => new { c.CapabilityId, c.Name })
            .Select(g => new TenantCourseMediaBreakdownResponse
            {
                CourseId = g.Key.CapabilityId,
                Name = g.Key.Name,
                CourseType = "Capability",
                ImagesCount = g.Count(),
                VideosCount = 0,
            })
            .ToListAsync(cancellationToken);

        var structuredLearningMedia = await _dbContext.StructuredLearningIllustrations
            .Join(_dbContext.StructuredLearningDocumentSections, i => i.StructuredLearningDocumentSectionId, s => s.StructuredLearningDocumentSectionId, (i, s) => new { i.MediaType, s.StructuredLearningDocumentId })
            .Join(_dbContext.StructuredLearningDocuments.Where(d => d.TenantId == tenantId), x => x.StructuredLearningDocumentId, d => d.StructuredLearningDocumentId, (x, d) => new { x.MediaType, d.StructuredLearningDocumentId, d.DocumentName })
            .GroupBy(x => new { x.StructuredLearningDocumentId, x.DocumentName })
            .Select(g => new TenantCourseMediaBreakdownResponse
            {
                CourseId = g.Key.StructuredLearningDocumentId,
                Name = g.Key.DocumentName,
                CourseType = "StructuredLearning",
                ImagesCount = g.Count(x => x.MediaType != "Video"),
                VideosCount = g.Count(x => x.MediaType == "Video"),
            })
            .ToListAsync(cancellationToken);

        return capabilityMedia
            .Concat(structuredLearningMedia)
            .Where(c => c.ImagesCount > 0 || c.VideosCount > 0)
            .OrderByDescending(c => c.ImagesCount + c.VideosCount)
            .Take(top)
            .ToList();
    }
}
