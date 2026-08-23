using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Knowledge Graph — Master de Clases de Entidad (2026-08-14, Fase 1b).
/// Plain catalog CRUD over <see cref="StructuredLearningEntityClass"/> —
/// no LLM/extraction wiring yet (that's a later step). Mirrors
/// <see cref="StructuredLearningEntityExtractionService.Normalize"/>'s
/// exact-match discipline: one row per (TenantId, SubjectId, NormalizedName).
/// </summary>
public sealed class StructuredLearningEntityClassService
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public StructuredLearningEntityClassService(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>Same normalization rule as <see cref="StructuredLearningEntityExtractionService.Normalize"/>
    /// — lowercased, trimmed, internal whitespace collapsed.</summary>
    public static string Normalize(string value) =>
        string.Join(' ', value.Trim().ToLowerInvariant().Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Returns every class already catalogued for this domain —
    /// the list an extraction agent should be given as reusable context.</summary>
    public async Task<List<StructuredLearningEntityClass>> GetForDomainAsync(
        Guid tenantId, Guid subjectId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.StructuredLearningEntityClasses
            .Where(c => c.TenantId == tenantId && c.SubjectId == subjectId)
            .OrderBy(c => c.DisplayLabel)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Looks up an existing class by exact normalized name first;
    /// only creates a new catalog row if none exists yet for this domain.
    /// Idempotent — safe to call repeatedly with the same name.</summary>
    public async Task<StructuredLearningEntityClass> FindOrCreateAsync(
        Guid tenantId, Guid subjectId, string name, string displayLabel, string genericDescription,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(name);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await dbContext.StructuredLearningEntityClasses.FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.SubjectId == subjectId && c.NormalizedName == normalized,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var entityClass = new StructuredLearningEntityClass
        {
            StructuredLearningEntityClassId = Guid.NewGuid(),
            TenantId = tenantId,
            SubjectId = subjectId,
            Name = name,
            DisplayLabel = displayLabel,
            GenericDescription = genericDescription,
            NormalizedName = normalized,
            CreatedDate = DateTime.UtcNow
        };
        dbContext.StructuredLearningEntityClasses.Add(entityClass);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entityClass;
    }
}
