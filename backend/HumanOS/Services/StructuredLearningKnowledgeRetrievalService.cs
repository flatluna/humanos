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
    /// with far more citations than any answer needs. Kept small (2026-09-04
    /// tightened from 6) — a single question should be answerable from a
    /// handful of engrams, not nearly a whole course.</summary>
    private const int MaxEngramsPerRetrieval = 3;

    /// <summary>Below this cosine similarity (question vs. the engram
    /// section's own text), a candidate section is dropped entirely —
    /// being merely LINKED to a matched entity (e.g. a keyword/exact-name
    /// hit) is not enough; the section's actual content must be reasonably
    /// close to what was asked, or it gets excluded instead of padding the
    /// result up to <see cref="MaxEngramsPerRetrieval"/> with irrelevant
    /// engrams that just happen to mention the same entity. Raised
    /// (2026-09-04) from 0.6 — too permissive for a small, single-topic
    /// course where almost every section scored above it for a broad
    /// question (e.g. "What is X?" against a whole "X" course).</summary>
    private const double MinEngramSimilarity = 0.72;

    /// <summary>How many top-heuristic-scored candidates to offer the LLM
    /// reranker (2026-09-04) — wider than <see cref="MaxEngramsPerRetrieval"/>
    /// so the reranker has real alternatives to choose from, but still small
    /// enough to keep the reranking call cheap.</summary>
    private const int MaxCandidatesForReranking = 8;

    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly CapabilityEmbeddingService _embeddingService;
    private readonly EngramRelevanceRerankerAgent _rerankerAgent;

    public StructuredLearningKnowledgeRetrievalService(
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        CapabilityEmbeddingService embeddingService,
        EngramRelevanceRerankerAgent rerankerAgent)
    {
        _dbContextFactory = dbContextFactory;
        _embeddingService = embeddingService;
        _rerankerAgent = rerankerAgent;
    }

    public async Task<StructuredLearningRetrievalPackage> BuildRetrievalPackageAsync(
        Guid tenantId,
        string userQuery,
        CancellationToken cancellationToken,
        Guid? subjectId = null,
        Guid? topicId = null,
        Guid? documentId = null,
        IReadOnlyList<Guid>? documentIds = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var normalizedQuery = StructuredLearningEntityExtractionService.Normalize(userQuery);

        var targetDocIds = documentIds is { Count: > 0 }
            ? documentIds.ToHashSet()
            : documentId.HasValue
                ? new HashSet<Guid> { documentId.Value }
                : null;

        IQueryable<StructuredLearningEntity> entitiesQuery;
        if (targetDocIds is not null)
        {
            var documentEntityIds = dbContext.StructuredLearningEntitySections
                .Where(l => targetDocIds.Contains(l.Section.StructuredLearningDocumentId))
                .Select(l => l.StructuredLearningEntityId);
            entitiesQuery = dbContext.StructuredLearningEntities.Where(e => documentEntityIds.Contains(e.StructuredLearningEntityId));
        }
        else
        {
            entitiesQuery = dbContext.StructuredLearningEntities.Where(e => e.TenantId == tenantId || e.TenantId == Guid.Empty);
            if (subjectId.HasValue) entitiesQuery = entitiesQuery.Where(e => e.SubjectId == subjectId.Value);
            if (topicId.HasValue) entitiesQuery = entitiesQuery.Where(e => e.TopicId == topicId.Value);
        }

        var allEntities = await entitiesQuery.ToListAsync(cancellationToken);

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

        var (retrievedEngrams, retrievedSchemas) = await RetrieveEngramsAndSchemasAsync(
            dbContext, matchedEntityIds, cancellationToken, queryEmbedding, userQuery, _rerankerAgent, documentId: documentId, documentIds: targetDocIds);

        // Hop 1 (2026-09-04 / 2026-09-05): entities co-mentioned in the retrieved
        // engrams' own sections. Only runs for Topic-level agents (no documentId or documentIds)
        // to discover related courses in the domain. For Course/Program-level agents,
        // all course sections are already in the direct candidate pool.
        List<RetrievedEntityDto> relatedEntities = [];
        List<RetrievedEngramDto> relatedEngrams = [];
        List<RetrievedSchemaDto> relatedSchemas = [];
        if (targetDocIds is null && retrievedEngrams.Count > 0)
        {
            var inScopeEntityIds = allEntities.Select(e => e.StructuredLearningEntityId).ToHashSet();
            var retrievedSectionIds = retrievedEngrams.Select(e => e.EngramId).ToHashSet();
            var relatedEntityLinks = await dbContext.StructuredLearningEntitySections
                .Where(l => retrievedSectionIds.Contains(l.StructuredLearningDocumentSectionId) && !matchedEntityIds.Contains(l.StructuredLearningEntityId))
                .Select(l => l.StructuredLearningEntityId)
                .Distinct()
                .ToListAsync(cancellationToken);
            relatedEntityLinks = relatedEntityLinks.Where(id => inScopeEntityIds.Contains(id)).Take(MaxRelatedEntities).ToList();

            relatedEntities = await dbContext.StructuredLearningEntities
                .Where(e => relatedEntityLinks.Contains(e.StructuredLearningEntityId))
                .Select(e => new RetrievedEntityDto
                {
                    EntityId = e.StructuredLearningEntityId,
                    CanonicalName = e.Name,
                    EntityType = e.EntityType,
                    MatchType = "Related"
                })
                .ToListAsync(cancellationToken);

            var (hopEngrams, hopSchemas) = await RetrieveEngramsAndSchemasAsync(
                dbContext, relatedEntityLinks.ToHashSet(), cancellationToken, queryEmbedding, userQuery, _rerankerAgent, excludeSectionIds: retrievedSectionIds, documentId: documentId, documentIds: targetDocIds);
            relatedEngrams = hopEngrams;
            relatedSchemas = hopSchemas;
        }

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
        string userQuery,
        EngramRelevanceRerankerAgent rerankerAgent,
        IReadOnlySet<Guid>? excludeSectionIds = null,
        Guid? documentId = null,
        IReadOnlySet<Guid>? documentIds = null)
    {
        List<StructuredLearningEntitySection> links;
        var candidateSectionsQuery = dbContext.StructuredLearningDocumentSections.AsQueryable();

        var scopedDocIds = documentIds is { Count: > 0 }
            ? documentIds
            : documentId.HasValue
                ? new HashSet<Guid> { documentId.Value }
                : null;

        if (scopedDocIds is not null)
        {
            // Course-level or Program-level agent: the candidate pool is ALL
            // sections of the target document(s).
            candidateSectionsQuery = candidateSectionsQuery
                .Where(s => scopedDocIds.Contains(s.StructuredLearningDocumentId));

            if (excludeSectionIds is not null && excludeSectionIds.Count > 0)
            {
                candidateSectionsQuery = candidateSectionsQuery
                    .Where(s => !excludeSectionIds.Contains(s.StructuredLearningDocumentSectionId));
            }

            links = await dbContext.StructuredLearningEntitySections
                .Where(l => scopedDocIds.Contains(l.Section.StructuredLearningDocumentId))
                .Select(l => new StructuredLearningEntitySection
                {
                    StructuredLearningEntityId = l.StructuredLearningEntityId,
                    StructuredLearningDocumentSectionId = l.StructuredLearningDocumentSectionId,
                    SourceText = l.SourceText
                })
                .ToListAsync(cancellationToken);
        }
        else
        {
            if (entityIds.Count == 0)
            {
                return ([], []);
            }

            links = await dbContext.StructuredLearningEntitySections
                .Where(l => entityIds.Contains(l.StructuredLearningEntityId))
                .Select(l => new StructuredLearningEntitySection
                {
                    StructuredLearningEntityId = l.StructuredLearningEntityId,
                    StructuredLearningDocumentSectionId = l.StructuredLearningDocumentSectionId,
                    SourceText = l.SourceText
                })
                .ToListAsync(cancellationToken);

            var candidateSectionIds = links.Select(l => l.StructuredLearningDocumentSectionId).Distinct()
                .Where(id => excludeSectionIds is null || !excludeSectionIds.Contains(id))
                .ToList();
            if (candidateSectionIds.Count == 0)
            {
                return ([], []);
            }

            candidateSectionsQuery = candidateSectionsQuery
                .Where(s => candidateSectionIds.Contains(s.StructuredLearningDocumentSectionId));
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

        if (candidateSections.Count == 0)
        {
            return ([], []);
        }

        // Rank by closeness to the actual question when a query embedding is
        // available — without this, every section merely LINKED to a matched
        // entity was returned in arbitrary order, flooding the agent with
        // far more (and less relevant) citations than the question needed.
        // ALSO drop anything below MinEngramSimilarity (2026-09-04 fix) —
        // previously the top N were kept regardless of how weak the match
        // was, so a keyword/exact-entity hit still dragged in every section
        // that merely mentions it, even ones that don't actually answer the
        // question.
        //
        // TITLE BOOST (2026-09-04, second fix): pure whole-section-text
        // embedding similarity is too FLAT to discriminate well inside a
        // small, single-topic course — every section talks about the same
        // subject in similar vocabulary, so e.g. "Conclusion" or "Responsible
        // Use" scored competitively against "Introduction" for a basic "What
        // is X?" question that the Introduction section actually answers.
        // Add a lexical bonus for sections whose OWN TITLE shares meaningful
        // words with the question — titles are short and specific, so this
        // reliably favors the section that is literally ABOUT what was asked
        // (e.g. "1. Introduction: Why X Matters", "2. What Is an X?") over
        // ones that merely happen to embed similarly.
        var directMatchedSectionIds = links
            .Where(l => entityIds.Contains(l.StructuredLearningEntityId))
            .Select(l => l.StructuredLearningDocumentSectionId)
            .ToHashSet();

        var exactArticleMatchedSectionIds = new HashSet<Guid>();
        var strongLexicalMatchedSectionIds = new HashSet<Guid>();

        var normalizedUserQuery = RemoveDiacritics(userQuery.Trim().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(normalizedUserQuery))
        {
            // Exact article detection: e.g. "articulo 1", "articulo 1.-", "art. 1", "art 7 bis"
            var articleRegex = new System.Text.RegularExpressions.Regex(
                @"\b(?:articulo|art\.?)\s*([0-9]+(?:\s*(?:bis|ter|quater))?)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var articleMatch = articleRegex.Match(normalizedUserQuery);

            foreach (var s in candidateSections)
            {
                var titleLower = RemoveDiacritics(s.SubchapterTitle.ToLowerInvariant());

                if (articleMatch.Success)
                {
                    var targetNum = articleMatch.Groups[1].Value.Trim().ToLowerInvariant();
                    var sectionArticleMatch = articleRegex.Match(titleLower);
                    if (sectionArticleMatch.Success && sectionArticleMatch.Groups[1].Value.Trim().ToLowerInvariant() == targetNum)
                    {
                        exactArticleMatchedSectionIds.Add(s.StructuredLearningDocumentSectionId);
                        directMatchedSectionIds.Add(s.StructuredLearningDocumentSectionId);
                        continue;
                    }
                }

                if (titleLower.Contains(normalizedUserQuery) || (normalizedUserQuery.Length >= 3 && titleLower.StartsWith(normalizedUserQuery)))
                {
                    directMatchedSectionIds.Add(s.StructuredLearningDocumentSectionId);
                }
            }

            // Verbatim/near-verbatim body lexical overlap (2026-09-07) — a
            // student who quotes or closely paraphrases actual document
            // wording (e.g. "vigilar el cumplimiento de las obligaciones de
            // la presentación de avisos...") should find that section even
            // when embedding similarity or the LLM reranker's judgment
            // happens to miss it; this is a deterministic safety net,
            // independent of both. Only fires when the question itself
            // contains enough distinctive words (>=4) to avoid false
            // positives on short/generic questions.
            var contentWords = normalizedUserQuery
                .Split([' ', ',', '.', ';', ':', '\n', '\r', '\t', '?', '¿', '!', '¡'], StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 5)
                .Distinct()
                .ToList();

            if (contentWords.Count >= 4)
            {
                var requiredMatches = Math.Max(4, (int)Math.Ceiling(contentWords.Count * 0.6));
                foreach (var s in candidateSections)
                {
                    if (directMatchedSectionIds.Contains(s.StructuredLearningDocumentSectionId))
                    {
                        continue;
                    }

                    var bodyLower = RemoveDiacritics(s.Text.ToLowerInvariant());
                    var matchedWords = contentWords.Count(w => bodyLower.Contains(w));
                    if (matchedWords >= requiredMatches)
                    {
                        directMatchedSectionIds.Add(s.StructuredLearningDocumentSectionId);
                        strongLexicalMatchedSectionIds.Add(s.StructuredLearningDocumentSectionId);
                    }
                }
            }
        }

        var rankedSections = queryEmbedding is not null
            ? candidateSections
                .Select(s => (
                    Section: s,
                    Similarity: s.Embedding.Memory.IsEmpty ? 0.0 : CosineSimilarity(queryEmbedding, s.Embedding.Memory.ToArray()),
                    TitleBoost: TitleRelevanceBoost(userQuery, s.SubchapterTitle),
                    IsDirectEntityMatch: directMatchedSectionIds.Contains(s.StructuredLearningDocumentSectionId),
                    IsExactArticleMatch: exactArticleMatchedSectionIds.Contains(s.StructuredLearningDocumentSectionId)))
                .Where(x => x.IsExactArticleMatch || x.IsDirectEntityMatch || (x.Similarity + x.TitleBoost) >= MinEngramSimilarity)
                .OrderByDescending(x => (x.IsExactArticleMatch ? 10.0 : 0.0) + (x.IsDirectEntityMatch ? 1.0 : 0.0) + x.TitleBoost + x.Similarity)
                .Select(x => x.Section)
            : candidateSections.AsEnumerable();
        var candidatePool = rankedSections.Take(MaxCandidatesForReranking).ToList();

        // If candidate pool was too aggressively filtered, fall back to top candidates with title boost
        if (candidatePool.Count == 0 && candidateSections.Count > 0)
        {
            candidatePool = candidateSections
                .OrderByDescending(s => TitleRelevanceBoost(userQuery, s.SubchapterTitle))
                .Take(MaxCandidatesForReranking)
                .ToList();
        }

        // LLM RERANK (2026-09-04 / 2026-09-05): the heuristic above is fast
        // to build a candidate pool, but may still include topically adjacent
        // sections that don't directly answer what was asked. The reranker agent
        // evaluates the candidate excerpts with a strict >=80% confidence threshold,
        // returning ONLY the high-confidence direct answers.
        List<int>? rerankedOrder = null;
        var rerankAttempted = false;
        if (rerankerAgent.IsConfigured && !string.IsNullOrWhiteSpace(userQuery) && candidatePool.Count > 0)
        {
            try
            {
                var rerankRequest = new EngramRerankRequest
                {
                    Question = userQuery,
                    MaxResults = MaxEngramsPerRetrieval,
                    Candidates = candidatePool.Select((s, i) => new EngramRerankCandidate
                    {
                        Index = i,
                        Title = s.SubchapterTitle,
                        Excerpt = s.Text.Length > 700 ? s.Text[..700] : s.Text
                    }).ToList()
                };
                var rerankResponse = await rerankerAgent.RerankAsync(rerankRequest, cancellationToken);
                rerankedOrder = rerankResponse.RelevantIndices;
                rerankAttempted = true;
            }
            catch
            {
                // Fall through to the heuristic-only result below on API failure
            }
        }

        var sections = rerankAttempted && rerankedOrder is not null && rerankedOrder.Count > 0
            ? rerankedOrder.Where(i => i >= 0 && i < candidatePool.Count).Select(i => candidatePool[i]).ToList()
            : candidatePool.Take(MaxEngramsPerRetrieval).ToList();

        // If the user specifically queried a numerical article/section, or quoted/closely
        // paraphrased actual document wording, ALWAYS guarantee that match is present in
        // the final retrieved sections — regardless of what the LLM reranker decided.
        var guaranteedMatchedSectionIds = exactArticleMatchedSectionIds.Concat(strongLexicalMatchedSectionIds).ToHashSet();
        if (guaranteedMatchedSectionIds.Count > 0)
        {
            var guaranteedSections = candidatePool
                .Where(s => guaranteedMatchedSectionIds.Contains(s.StructuredLearningDocumentSectionId))
                .ToList();

            if (guaranteedSections.Count > 0)
            {
                var existingIds = sections.Select(s => s.StructuredLearningDocumentSectionId).ToHashSet();
                var missing = guaranteedSections.Where(s => !existingIds.Contains(s.StructuredLearningDocumentSectionId)).ToList();
                sections.InsertRange(0, missing);
                if (sections.Count > 5)
                {
                    sections = sections.Take(5).ToList();
                }
            }
        }

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

    /// <summary>Common short function words excluded from the title-overlap
    /// check below — matching on "the"/"is"/"a" would boost almost every
    /// section indiscriminately and defeat the purpose of the boost.</summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "is", "are", "was", "were", "what", "why", "how", "who", "when", "where",
        "of", "to", "in", "on", "for", "and", "or", "with", "this", "that", "it", "its", "your", "you",
        "el", "la", "los", "las", "un", "una", "es", "son", "qu\u00e9", "por", "de", "en", "y", "o", "con",
        "esta", "esto", "para", "tu", "su"
    };

    /// <summary>Lexical relevance bonus (0 to ~0.3) added on top of embedding
    /// cosine similarity when ranking candidate engrams — see the ranking
    /// comment above for why pure whole-section embeddings are too flat to
    /// tell "Introduction" from "Conclusion" inside a single-topic course.
    /// Counts how many of the question's own meaningful words (4+ letters,
    /// not a stop word) literally or fuzzily appear in the section's own short title —
    /// titles are specific enough that this reliably favors the section
    /// that is actually ABOUT what was asked.</summary>
    private static double TitleRelevanceBoost(string userQuery, string title)
    {
        if (string.IsNullOrWhiteSpace(userQuery) || string.IsNullOrWhiteSpace(title))
        {
            return 0;
        }

        var normalizedQuery = userQuery.Trim().ToLowerInvariant();
        var normalizedTitle = title.Trim().ToLowerInvariant();

        if (normalizedTitle.Contains(normalizedQuery) || normalizedQuery.Contains(normalizedTitle))
        {
            return 0.45;
        }

        var queryWords = userQuery
            .Split([' ', '\t', '\n', '\r', ',', '.', '?', '!', ':', ';', '"', '\''], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => (w.Length >= 1 && w.All(char.IsDigit)) || (w.Length >= 3 && !StopWords.Contains(w)))
            .Select(w => w.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (queryWords.Count == 0)
        {
            return 0;
        }

        var titleWords = title
            .Split([' ', '\t', '\n', '\r', ',', '.', '?', '!', ':', ';', '"', '\'', '-', '/', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => (w.Length >= 1 && w.All(char.IsDigit)) || (w.Length >= 3 && !StopWords.Contains(w)))
            .Select(w => w.ToLowerInvariant())
            .Distinct()
            .ToList();

        var matches = queryWords.Count(qw => titleWords.Any(tw => IsFuzzyWordMatch(qw, tw)));

        // Capped so a handful of shared words gives a meaningful nudge
        // without letting the lexical signal completely override semantics.
        return Math.Min(0.4, matches * 0.15);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; d[i, 0] = i++) ;
        for (int j = 0; j <= m; d[0, j] = j++) ;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    private static bool IsFuzzyWordMatch(string queryWord, string titleWord)
    {
        if (queryWord.Equals(titleWord, StringComparison.OrdinalIgnoreCase)) return true;
        if (titleWord.Contains(queryWord, StringComparison.OrdinalIgnoreCase) || queryWord.Contains(titleWord, StringComparison.OrdinalIgnoreCase)) return true;

        var minLen = Math.Min(queryWord.Length, titleWord.Length);
        var maxLen = Math.Max(queryWord.Length, titleWord.Length);
        if (maxLen - minLen > 2) return false;

        var dist = LevenshteinDistance(queryWord.ToLowerInvariant(), titleWord.ToLowerInvariant());
        if (minLen >= 7 && dist <= 2) return true;
        if (minLen >= 4 && dist <= 1) return true;
        return false;
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}
