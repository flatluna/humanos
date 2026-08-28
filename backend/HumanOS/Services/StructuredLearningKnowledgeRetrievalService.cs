using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

// ==================== Retrieval package DTOs (2026-08-14) ====================
// Conceptually mirrors the Knowledge Graph design's "retrieval package" —
// what gets handed to an intelligent agent for explanation.

public sealed class RetrievedEntityDto
{
    public Guid EntityId { get; set; }

    public string CanonicalName { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    /// <summary>"Exact" / "Alias" / "Semantic" — which retrieval path matched this entity.</summary>
    public string MatchType { get; set; } = string.Empty;
}

public sealed class RetrievedEngramDto
{
    public Guid EngramId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public Guid SchemaId { get; set; }

    public string SchemaName { get; set; } = string.Empty;

    public List<string> MatchingSourceText { get; set; } = [];
}

public sealed class RetrievedSchemaDto
{
    public Guid SchemaId { get; set; }

    public string SchemaName { get; set; } = string.Empty;
}

/// <summary>The full package handed to an agent for explanation — Hop 0
/// (directly matched engrams/schemas) plus Hop 1 (entities co-mentioned in
/// those same engrams, and THEIR OWN engrams/schemas) as separate,
/// clearly-labeled fields so the agent can decide whether to follow up —
/// see the "2 saltos, perezoso" retrieval design.</summary>
public sealed class StructuredLearningRetrievalPackage
{
    public string UserQuery { get; set; } = string.Empty;

    public List<RetrievedEntityDto> MatchedEntities { get; set; } = [];

    public List<RetrievedEngramDto> RetrievedEngrams { get; set; } = [];

    public List<RetrievedSchemaDto> Schemas { get; set; } = [];

    /// <summary>Hop-1 entities co-mentioned in the retrieved engrams —
    /// "hubs" the agent MAY choose to explore next, each as a fresh query.</summary>
    public List<RetrievedEntityDto> RelatedEntities { get; set; } = [];

    /// <summary>Hop-1 engrams reached via <see cref="RelatedEntities"/> —
    /// kept separate from <see cref="RetrievedEngrams"/> so the agent knows
    /// these are one step removed from the direct match.</summary>
    public List<RetrievedEngramDto> RelatedEngrams { get; set; } = [];
}

/// <summary>
/// Knowledge Graph (2026-08-14) — implements the retrieval flow: exact
/// canonical match -&gt; alias match -&gt; semantic (embedding) fallback,
/// then follows entity-to-engram links to retrieve engrams + their schemas,
/// plus one "hop" of related entities/engrams for associative exploration.
/// Exact/alias matching is never replaced by vector search — vector search
/// only runs when neither found a sufficiently strong match.
/// </summary>
public sealed class StructuredLearningKnowledgeRetrievalService
{
    /// <summary>Below this cosine similarity, a semantic match is
    /// considered too weak to surface.</summary>
    private const double MinSemanticSimilarity = 0.75;

    /// <summary>How many Hop-1 related entities/engrams to surface at
    /// most — keeps the package small and readable instead of spidering
    /// into the whole catalog (see the "1 salto" retrieval design note).</summary>
    private const int MaxRelatedEntities = 5;

    /// <summary>Hard cap on how many Engram sections one retrieval call
    /// (retrieved OR related) can return — without this, a query matching a
    /// broad/common entity (linked to many sections) flooded the chat agent
    /// with far more citations than any answer needs.</summary>
    private const int MaxEngramsPerRetrieval = 6;

    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly CapabilityEmbeddingService _embeddingService;

    public StructuredLearningKnowledgeRetrievalService(
        IDbContextFactory<HumanOsDbContext> dbContextFactory, CapabilityEmbeddingService embeddingService)
    {
        _dbContextFactory = dbContextFactory;
        _embeddingService = embeddingService;
    }

    public async Task<StructuredLearningRetrievalPackage> BuildRetrievalPackageAsync(
        Guid tenantId,
        string userQuery,
        CancellationToken cancellationToken,
        Guid? subjectId = null,
        Guid? topicId = null,
        Guid? documentId = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var normalizedQuery = StructuredLearningEntityExtractionService.Normalize(userQuery);
        var entitiesQuery = dbContext.StructuredLearningEntities.Where(e => e.TenantId == tenantId);

        // Topic-level agent (e.g. "Matemáticas/Álgebra") — every course under
        // that Topic. Course-level agent (documentId set, e.g. one specific
        // course) narrows further below, after Engrams are resolved, since
        // entities themselves are scoped by Subject/Topic, not by document.
        if (subjectId.HasValue)
        {
            entitiesQuery = entitiesQuery.Where(e => e.SubjectId == subjectId.Value);
        }
        if (topicId.HasValue)
        {
            entitiesQuery = entitiesQuery.Where(e => e.TopicId == topicId.Value);
        }

        var allEntities = await entitiesQuery.ToListAsync(cancellationToken);

        // Course-level agent: only entities actually mentioned in THIS
        // document's own sections are eligible candidates.
        if (documentId.HasValue)
        {
            var documentEntityIds = await dbContext.StructuredLearningEntitySections
                .Where(l => l.Section.StructuredLearningDocumentId == documentId.Value)
                .Select(l => l.StructuredLearningEntityId)
                .Distinct()
                .ToListAsync(cancellationToken);
            allEntities = allEntities.Where(e => documentEntityIds.Contains(e.StructuredLearningEntityId)).ToList();
        }

        var matched = new List<(StructuredLearningEntity Entity, string MatchType)>();

        // Computed once and reused both for the Semantic entity-matching
        // fallback below AND to rank retrieved/related Engrams by actual
        // closeness to the question (instead of arbitrary link order) —
        // see RetrieveEngramsAndSchemasAsync's queryEmbedding parameter.
        float[]? queryEmbedding = _embeddingService.IsConfigured
            ? await _embeddingService.GenerateEmbeddingAsync(userQuery, cancellationToken)
            : null;

        // 1) Exact canonical match.
        matched.AddRange(allEntities
            .Where(e => e.NormalizedValue == normalizedQuery)
            .Select(e => (e, "Exact")));

        // 2) Alias match — only for entities not already matched exactly.
        if (matched.Count == 0)
        {
            matched.AddRange(allEntities
                .Where(e => e.Aliases.Any(a => StructuredLearningEntityExtractionService.Normalize(a) == normalizedQuery))
                .Select(e => (e, "Alias")));
        }

        // 3) Semantic fallback — only when no exact/alias match exists.
        if (matched.Count == 0 && queryEmbedding is not null)
        {
            matched.AddRange(allEntities
                .Where(e => e.Embedding is not null)
                .Select(e => (Entity: e, Similarity: CosineSimilarity(queryEmbedding, e.Embedding!.Value.Memory.ToArray())))
                .Where(x => x.Similarity >= MinSemanticSimilarity)
                .OrderByDescending(x => x.Similarity)
                .Take(MaxRelatedEntities)
                .Select(x => (x.Entity, "Semantic")));
        }

        var matchedEntityIds = matched.Select(m => m.Entity.StructuredLearningEntityId).ToHashSet();
        var matchedEntities = matched
            .Select(m => new RetrievedEntityDto
            {
                EntityId = m.Entity.StructuredLearningEntityId,
                CanonicalName = m.Entity.Name,
                EntityType = m.Entity.EntityType,
                MatchType = m.MatchType
            })
            .ToList();

        var (retrievedEngrams, retrievedSchemas) = await RetrieveEngramsAndSchemasAsync(dbContext, matchedEntityIds, cancellationToken, queryEmbedding, documentId: documentId);

        // Hop 1: entities co-mentioned in the retrieved engrams' own
        // sections, excluding the ones already directly matched. Restricted
        // to the same allEntities scope (Subject/Topic/document) so a
        // Course-level agent never wanders into another course's entities.
        var inScopeEntityIds = allEntities.Select(e => e.StructuredLearningEntityId).ToHashSet();
        var retrievedSectionIds = retrievedEngrams.Select(e => e.EngramId).ToHashSet();
        var relatedEntityLinks = await dbContext.StructuredLearningEntitySections
            .Where(l => retrievedSectionIds.Contains(l.StructuredLearningDocumentSectionId) && !matchedEntityIds.Contains(l.StructuredLearningEntityId))
            .Select(l => l.StructuredLearningEntityId)
            .Distinct()
            .ToListAsync(cancellationToken);
        relatedEntityLinks = relatedEntityLinks.Where(id => inScopeEntityIds.Contains(id)).Take(MaxRelatedEntities).ToList();

        var relatedEntities = await dbContext.StructuredLearningEntities
            .Where(e => relatedEntityLinks.Contains(e.StructuredLearningEntityId))
            .Select(e => new RetrievedEntityDto
            {
                EntityId = e.StructuredLearningEntityId,
                CanonicalName = e.Name,
                EntityType = e.EntityType,
                MatchType = "Related"
            })
            .ToListAsync(cancellationToken);

        var (relatedEngrams, relatedSchemas) = await RetrieveEngramsAndSchemasAsync(
            dbContext, relatedEntityLinks.ToHashSet(), cancellationToken, queryEmbedding, excludeSectionIds: retrievedSectionIds, documentId: documentId);

        var schemas = retrievedSchemas.Concat(relatedSchemas)
            .GroupBy(s => s.SchemaId)
            .Select(g => g.First())
            .ToList();

        return new StructuredLearningRetrievalPackage
        {
            UserQuery = userQuery,
            MatchedEntities = matchedEntities,
            RetrievedEngrams = retrievedEngrams,
            Schemas = schemas,
            RelatedEntities = relatedEntities,
            RelatedEngrams = relatedEngrams
        };
    }

    private static async Task<(List<RetrievedEngramDto> Engrams, List<RetrievedSchemaDto> Schemas)> RetrieveEngramsAndSchemasAsync(
        HumanOsDbContext dbContext,
        IReadOnlySet<Guid> entityIds,
        CancellationToken cancellationToken,
        float[]? queryEmbedding,
        IReadOnlySet<Guid>? excludeSectionIds = null,
        Guid? documentId = null)
    {
        if (entityIds.Count == 0)
        {
            return ([], []);
        }

        var links = await dbContext.StructuredLearningEntitySections
            .Where(l => entityIds.Contains(l.StructuredLearningEntityId))
            .Select(l => new { l.StructuredLearningDocumentSectionId, l.SourceText })
            .ToListAsync(cancellationToken);

        var candidateSectionIds = links.Select(l => l.StructuredLearningDocumentSectionId).Distinct()
            .Where(id => excludeSectionIds is null || !excludeSectionIds.Contains(id))
            .ToList();
        if (candidateSectionIds.Count == 0)
        {
            return ([], []);
        }

        var candidateSectionsQuery = dbContext.StructuredLearningDocumentSections
            .Where(s => candidateSectionIds.Contains(s.StructuredLearningDocumentSectionId));
        if (documentId.HasValue)
        {
            candidateSectionsQuery = candidateSectionsQuery.Where(s => s.StructuredLearningDocumentId == documentId.Value);
        }

        var candidateSections = await candidateSectionsQuery
            .Select(s => new
            {
                s.StructuredLearningDocumentSectionId,
                s.SubchapterTitle,
                s.Text,
                s.StructuredLearningDocumentId,
                DocumentName = s.Document.DocumentName,
                s.Embedding
            })
            .ToListAsync(cancellationToken);

        // Rank by closeness to the actual question when a query embedding is
        // available — without this, every section merely LINKED to a matched
        // entity was returned in arbitrary order, flooding the agent with
        // far more (and less relevant) citations than the question needed.
        var sections = (queryEmbedding is not null
                ? candidateSections.OrderByDescending(s => CosineSimilarity(queryEmbedding, s.Embedding.Memory.ToArray()))
                : candidateSections.AsEnumerable())
            .Take(MaxEngramsPerRetrieval)
            .ToList();

        var engrams = sections.Select(s => new RetrievedEngramDto
        {
            EngramId = s.StructuredLearningDocumentSectionId,
            Title = s.SubchapterTitle,
            Text = s.Text,
            SchemaId = s.StructuredLearningDocumentId,
            SchemaName = s.DocumentName,
            MatchingSourceText = links
                .Where(l => l.StructuredLearningDocumentSectionId == s.StructuredLearningDocumentSectionId)
                .Select(l => l.SourceText)
                .Distinct()
                .ToList()
        }).ToList();

        var schemas = sections
            .Select(s => new RetrievedSchemaDto { SchemaId = s.StructuredLearningDocumentId, SchemaName = s.DocumentName })
            .GroupBy(s => s.SchemaId)
            .Select(g => g.First())
            .ToList();

        return (engrams, schemas);
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
        {
            return 0;
        }

        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
