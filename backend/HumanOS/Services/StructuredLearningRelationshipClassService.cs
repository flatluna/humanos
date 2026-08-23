using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Knowledge Graph — Master de Clases de Arista (2026-08-15, Fase 3).
/// Plain catalog CRUD over <see cref="StructuredLearningRelationshipClass"/>
/// — mirrors <see cref="StructuredLearningEntityClassService"/> exactly,
/// same normalization/dedup discipline, one row per
/// (TenantId, SubjectId, NormalizedLabel).
/// </summary>
public sealed class StructuredLearningRelationshipClassService
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public StructuredLearningRelationshipClassService(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public static string Normalize(string value) =>
        string.Join(' ', value.Trim().ToLowerInvariant().Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Returns every relationship class already catalogued for
    /// this domain — the list a connection extraction agent should be
    /// given as reusable context.</summary>
    public async Task<List<StructuredLearningRelationshipClass>> GetForDomainAsync(
        Guid tenantId, Guid subjectId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.StructuredLearningRelationshipClasses
            .Where(c => c.TenantId == tenantId && c.SubjectId == subjectId)
            .OrderBy(c => c.Label)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Looks up an existing class by exact normalized label first;
    /// only creates a new catalog row if none exists yet for this domain.
    /// Idempotent — safe to call repeatedly with the same label.</summary>
    public async Task<StructuredLearningRelationshipClass> FindOrCreateAsync(
        Guid tenantId, Guid subjectId, string label, string category, string genericDescription,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(label);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await dbContext.StructuredLearningRelationshipClasses.FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.SubjectId == subjectId && c.NormalizedLabel == normalized,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var relationshipClass = new StructuredLearningRelationshipClass
        {
            StructuredLearningRelationshipClassId = Guid.NewGuid(),
            TenantId = tenantId,
            SubjectId = subjectId,
            Label = label,
            Category = category,
            GenericDescription = genericDescription,
            NormalizedLabel = normalized,
            CreatedDate = DateTime.UtcNow
        };
        dbContext.StructuredLearningRelationshipClasses.Add(relationshipClass);
        await dbContext.SaveChangesAsync(cancellationToken);
        return relationshipClass;
    }
}
