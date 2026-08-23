using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Knowledge Graph — Catálogo de Tipos/Temas (2026-08-16). Plain catalog
/// CRUD over <see cref="StructuredLearningTopic"/> — one Subject (domain)
/// has many Topics (e.g. "Leyes" -> "Ley del IVA", "Ley del ISR"). Used both
/// by the admin management page and by StructuredLearningEntityExtractionService's
/// dual scoping rule for master-registry candidates.
/// </summary>
public sealed class StructuredLearningTopicService
{
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public StructuredLearningTopicService(IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>Every Topic catalogued for one domain, ordered by name —
    /// used both by the admin page and by the "pick a Topic" step of
    /// document creation.</summary>
    public async Task<List<StructuredLearningTopic>> GetForSubjectAsync(
        Guid tenantId, Guid subjectId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.StructuredLearningTopics
            .Where(t => t.TenantId == tenantId && t.SubjectId == subjectId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<StructuredLearningTopic>> GetForTenantAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.StructuredLearningTopics
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<StructuredLearningTopic> CreateAsync(
        Guid tenantId, Guid subjectId, string name, string? description, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var topic = new StructuredLearningTopic
        {
            StructuredLearningTopicId = Guid.NewGuid(),
            TenantId = tenantId,
            SubjectId = subjectId,
            Name = name,
            Description = description,
            CreatedDate = DateTime.UtcNow
        };
        dbContext.StructuredLearningTopics.Add(topic);
        await dbContext.SaveChangesAsync(cancellationToken);
        return topic;
    }

    public async Task<StructuredLearningTopic?> UpdateAsync(
        Guid topicId, string name, string? description, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var topic = await dbContext.StructuredLearningTopics.FirstOrDefaultAsync(
            t => t.StructuredLearningTopicId == topicId, cancellationToken);
        if (topic is null)
        {
            return null;
        }

        topic.Name = name;
        topic.Description = description;
        topic.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return topic;
    }

    /// <summary>Returns false if the Topic doesn't exist — never throws for
    /// a missing row, same convention as the rest of Studio's admin CRUD.</summary>
    public async Task<bool> DeleteAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var topic = await dbContext.StructuredLearningTopics.FirstOrDefaultAsync(
            t => t.StructuredLearningTopicId == topicId, cancellationToken);
        if (topic is null)
        {
            return false;
        }

        dbContext.StructuredLearningTopics.Remove(topic);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
