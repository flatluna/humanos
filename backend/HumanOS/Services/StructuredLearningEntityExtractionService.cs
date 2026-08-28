using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Knowledge Graph (2026-08-14) — extracts entities from one
/// <see cref="StructuredLearningDocumentSection"/> ("Engram") at a time,
/// via <see cref="StructuredLearningEntityExtractionAgent"/>, then:
/// 1) finds-or-creates the canonical <see cref="StructuredLearningEntity"/>
///    catalog row (deduplicated per tenant by normalized name + type);
/// 2) links it to the section via <see cref="StructuredLearningEntitySection"/>,
///    preserving the exact source evidence.
///
/// Idempotent by design: re-running for the same section never creates a
/// duplicate catalog entity (unique index on TenantId+NormalizedValue+
/// EntityType) nor a duplicate link row (unique index on EntityId+SectionId)
/// — both are look-up-then-insert, matching every other Studio service's
/// resumable-run discipline. Never modifies <see cref="StructuredLearningDocumentSection.Text"/>.
/// </summary>
public sealed class StructuredLearningEntityExtractionService
{
    private readonly StructuredLearningEntityExtractionAgent _extractionAgent;
    private readonly CapabilityEmbeddingService _embeddingService;
    private readonly StructuredLearningGenerationUsageService _usageService;
    private readonly StructuredLearningEntityClassService _entityClassService;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public StructuredLearningEntityExtractionService(
        StructuredLearningEntityExtractionAgent extractionAgent,
        CapabilityEmbeddingService embeddingService,
        StructuredLearningGenerationUsageService usageService,
        StructuredLearningEntityClassService entityClassService,
        IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _extractionAgent = extractionAgent;
        _embeddingService = embeddingService;
        _usageService = usageService;
        _entityClassService = entityClassService;
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>Entity extraction itself is the hard requirement; the
    /// embedding call is best-effort (entity still gets created/linked
    /// without it — only semantic-fallback retrieval degrades).</summary>
    public bool IsConfigured => _extractionAgent.IsConfigured;

    /// <summary>Normalizes a raw entity/alias/query string for exact-match
    /// comparisons — lowercased, trimmed, internal whitespace collapsed.
    /// Public so retrieval can normalize a user query the same way.</summary>
    public static string Normalize(string value) =>
        string.Join(' ', value.Trim().ToLowerInvariant().Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Knowledge Graph Topic scoping (2026-08-16) — document-
    /// structural entity types (an article/section/document reference, NOT
    /// a general legal/domain concept) must be scoped by Topic to avoid
    /// e.g. "Artículo 1o." of two unrelated laws colliding just because
    /// both share the same broader SubjectId. Best-effort keyword match
    /// since EntityType is free text from the LLM, not a fixed enum.</summary>
    public static bool IsDocumentStructuralType(string entityType)
    {
        var t = entityType.ToLowerInvariant();
        return t.Contains("article") || t.Contains("legalreference") || t.Contains("documentsection") ||
               t.Contains("clause") || t == "document" || t.Contains("legaldocument");
    }

    /// <summary>Whether a master-registry candidate entity is in scope for
    /// THIS document's Topic — general concepts (non-structural) are always
    /// in scope (shared across the whole domain); structural entities
    /// (Article/LegalReference/...) require an EXACT TopicId match so
    /// "Artículo 1o." from a different law never becomes a match
    /// candidate.</summary>
    public static bool IsCandidateInScope(string candidateEntityType, Guid? candidateTopicId, Guid? documentTopicId) =>
        !IsDocumentStructuralType(candidateEntityType) || candidateTopicId == documentTopicId;

    private static bool IsEducationalContainer(string name, string entityType)
    {
        var normalizedName = Normalize(name).Replace(" ", string.Empty, StringComparison.Ordinal);
        var normalizedType = Normalize(entityType).Replace(" ", string.Empty, StringComparison.Ordinal);
        return normalizedName is "engram" or "learningmodule" or "learningunit" or "lesson" or "chapter" or "section" ||
               normalizedType is "engram" or "learningmodule" or "learningunit";
    }

    /// <summary>Extracts and persists entities for ONE section. Safe to
    /// call repeatedly (idempotent) — returns the number of NEW entity-to-
    /// section links created (0 if everything already existed or the
    /// section had no extractable entities).</summary>
    public async Task<int> ExtractForSectionAsync(Guid sectionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var section = await dbContext.StructuredLearningDocumentSections
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new InvalidOperationException($"No se encontró la sección '{sectionId}'.");

        // Master de Clases (2026-08-14, Fase 2): only meaningful when the
        // document has a domain (SubjectId) set — documents created before
        // this feature (or without a Subject picked) fall back to the
        // original free-text EntityType behavior.
        var document = await dbContext.StructuredLearningDocuments
            .Where(d => d.StructuredLearningDocumentId == section.StructuredLearningDocumentId)
            .Select(d => new { d.SubjectId, d.TopicId })
            .FirstOrDefaultAsync(cancellationToken);
        var subjectId = document?.SubjectId;
        var topicId = document?.TopicId;

        var existingClasses = subjectId is Guid domainId
            ? await _entityClassService.GetForDomainAsync(section.TenantId, domainId, cancellationToken)
            : [];

        // Master registry (2026-08-15, GraphRAG-style entity resolution):
        // the REAL canonical entities already saved for this domain, from
        // OTHER Engrams processed earlier (never from this section itself —
        // extraction stays strictly per-Engram). Fase 4 (2026-08-15): ranked
        // by semantic similarity to THIS section's own already-computed
        // Embedding — same VectorDistance pattern as
        // NodeKnowledgeIndexService.SearchAsync — instead of just "most
        // recent N", so the candidate list actually reflects what's
        // relevant to this section's text. Entities with no embedding yet
        // (legacy rows) are appended afterwards, most-recent-first, so
        // nothing is silently excluded from consideration. Short "m0",
        // "m1"... ids are generated here (never by the model) so the
        // response only ever has to echo back a short token instead of
        // repeating full entity names.
        const int semanticCandidateCount = 40;
        const int fallbackCandidateCount = 20;
        var masterEntities = new List<(Guid Id, string Name, List<string> Aliases)>();
        if (subjectId is Guid subject)
        {
            var semanticMatches = await dbContext.StructuredLearningEntities
                .Where(e => e.TenantId == section.TenantId && e.SubjectId == subject && e.Embedding != null)
                .OrderBy(e => EF.Functions.VectorDistance("cosine", e.Embedding!.Value, section.Embedding))
                .Take(semanticCandidateCount)
                .Select(e => new { e.StructuredLearningEntityId, e.Name, e.Aliases, e.EntityType, e.TopicId })
                .ToListAsync(cancellationToken);
            masterEntities.AddRange(semanticMatches
                .Where(e => IsCandidateInScope(e.EntityType, e.TopicId, topicId))
                .Select(e => (e.StructuredLearningEntityId, e.Name, e.Aliases)));

            var seenIds = masterEntities.Select(e => e.Id).ToHashSet();
            var noEmbeddingFallback = await dbContext.StructuredLearningEntities
                .Where(e => e.TenantId == section.TenantId && e.SubjectId == subject && e.Embedding == null && !seenIds.Contains(e.StructuredLearningEntityId))
                .OrderByDescending(e => e.CreatedDate)
                .Take(fallbackCandidateCount)
                .Select(e => new { e.StructuredLearningEntityId, e.Name, e.Aliases, e.EntityType, e.TopicId })
                .ToListAsync(cancellationToken);
            masterEntities.AddRange(noEmbeddingFallback
                .Where(e => IsCandidateInScope(e.EntityType, e.TopicId, topicId))
                .Select(e => (e.StructuredLearningEntityId, e.Name, e.Aliases)));
        }
        var masterIdMap = new Dictionary<string, Guid>();
        var masterCandidates = new List<(string Id, string Name, IReadOnlyList<string> Aliases)>();
        for (var i = 0; i < masterEntities.Count; i++)
        {
            var candidateId = $"m{i}";
            masterIdMap[candidateId] = masterEntities[i].Id;
            masterCandidates.Add((candidateId, masterEntities[i].Name, masterEntities[i].Aliases));
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var extractionResult = await _extractionAgent.ExtractAsync(
            section.SubchapterTitle, section.Text, existingClasses.Select(c => c.DisplayLabel).ToList(), masterCandidates, cancellationToken);
        stopwatch.Stop();

        try
        {
            await _usageService.RecordAsync(
                section.StructuredLearningDocumentId, sectionId, "KnowledgeGraph", "StructuredLearningEntityExtractionAgent",
                extractionResult.ModelName, extractionResult.InputTokens, extractionResult.OutputTokens,
                extractionResult.CachedInputTokens, stopwatch.ElapsedMilliseconds, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Usage logging must never break the actual extraction it measures.
        }

        var extraction = extractionResult.Response;
        extraction.Entities = MergeEquivalentExtractions(extraction.Entities);
        if (extraction.Entities.Count == 0)
        {
            return 0;
        }

        // Persistence retry (2026-08-18): sections are processed in
        // parallel (see StructuredLearningExtractionService's
        // Parallel.ForEachAsync), each with its own DbContext — two
        // sections can both decide the SAME new entity/alias doesn't exist
        // yet (neither has committed) and both try to insert it, tripping
        // the unique index on (TenantId, NormalizedValue, EntityType) at
        // SaveChangesAsync time ("An error occurred while saving the
        // entity changes"). Retrying with a BRAND NEW DbContext re-runs
        // ResolveEntityAsync's lookups, which will now find the row the
        // other task just committed and reuse it instead of re-inserting —
        // no re-call to the (expensive) extraction LLM needed.
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await PersistEntitiesAsync(sectionId, section.TenantId, subjectId, topicId, section.Text, extraction.Entities, masterIdMap, cancellationToken);
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken);
            }
        }

        return await PersistEntitiesAsync(sectionId, section.TenantId, subjectId, topicId, section.Text, extraction.Entities, masterIdMap, cancellationToken);
    }

    private async Task<int> PersistEntitiesAsync(
        Guid sectionId, Guid tenantId, Guid? subjectId, Guid? topicId, string sectionText,
        List<ExtractedEntityDto> entities, Dictionary<string, Guid> masterIdMap, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Tracks entity ids already added to THIS batch (2026-08-26 fix) —
        // the DB-only AnyAsync check below can't see rows added earlier in
        // this same loop (not yet flushed), so two extracted mentions that
        // both resolve to the same entity (e.g. two aliases of one already-
        // known entity) would both pass it and hit the unique index
        // (EntityId, SectionId) together at SaveChangesAsync.
        var linkedEntityIdsInBatch = new HashSet<Guid>();

        var linksCreated = 0;
        foreach (var extracted in entities)
        {
            if (string.IsNullOrWhiteSpace(extracted.CanonicalName) || string.IsNullOrWhiteSpace(extracted.SourceText) ||
                IsEducationalContainer(extracted.CanonicalName, extracted.EntityType))
            {
                continue;
            }

            var (entity, resolutionStatus, matchConfidence, matchReason) = await ResolveEntityAsync(
                dbContext, tenantId, subjectId, topicId, extracted, masterIdMap, cancellationToken);

            if (!linkedEntityIdsInBatch.Add(entity.StructuredLearningEntityId))
            {
                continue;
            }

            var alreadyLinked = await dbContext.StructuredLearningEntitySections.AnyAsync(
                l => l.StructuredLearningEntityId == entity.StructuredLearningEntityId &&
                     l.StructuredLearningDocumentSectionId == sectionId,
                cancellationToken);
            if (alreadyLinked)
            {
                continue;
            }

            var startPosition = sectionText.IndexOf(extracted.SourceText, StringComparison.Ordinal);
            dbContext.StructuredLearningEntitySections.Add(new StructuredLearningEntitySection
            {
                StructuredLearningEntitySectionId = Guid.NewGuid(),
                StructuredLearningEntityId = entity.StructuredLearningEntityId,
                StructuredLearningDocumentSectionId = sectionId,
                TenantId = tenantId,
                SourceText = extracted.SourceText,
                StartPosition = startPosition >= 0 ? startPosition : null,
                EndPosition = startPosition >= 0 ? startPosition + extracted.SourceText.Length : null,
                ResolutionStatus = resolutionStatus,
                MatchConfidence = matchConfidence,
                MatchReason = matchReason,
                CreatedDate = DateTime.UtcNow
            });
            linksCreated++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return linksCreated;
    }

    /// <summary>Resolves ONE extracted entity against the master registry
    /// for this domain — trusts the LLM's matched/new/ambiguous decision
    /// (<see cref="ExtractedEntityDto.ResolutionStatus"/>) FIRST, falling
    /// back to the original exact-normalized-name match only as a safety
    /// net (e.g. if the model picked "new" for an entity that in fact
    /// already exists verbatim). "ambiguous" entities still get a row (so
    /// the section keeps its evidence) but as a NEW entity — never merged
    /// automatically — so they can be reviewed/merged manually later.</summary>
    private async Task<(StructuredLearningEntity Entity, string ResolutionStatus, double? MatchConfidence, string? MatchReason)> ResolveEntityAsync(
        HumanOsDbContext dbContext, Guid tenantId, Guid? domainId, Guid? topicId, ExtractedEntityDto extracted,
        Dictionary<string, Guid> masterIdMap, CancellationToken cancellationToken)
    {
        var normalized = Normalize(extracted.CanonicalName);

        // Master de Clases (2026-08-14, Fase 2): resolve/create the reusable
        // class BEFORE the entity lookup, so a document with no domain set
        // simply skips this (EntityClassId stays null, same as before).
        Guid? entityClassId = null;
        if (domainId is Guid domain)
        {
            var entityClass = await _entityClassService.FindOrCreateAsync(
                tenantId, domain, extracted.EntityType, extracted.EntityType, extracted.EntityTypeDescription, cancellationToken);
            entityClassId = entityClass.StructuredLearningEntityClassId;
        }

        // LLM-mediated resolution (2026-08-15) — trust a confident "matched"
        // decision against a real candidate from the master registry.
        if (extracted.ResolutionStatus == "matched" &&
            extracted.Confidence >= 0.90 &&
            !string.IsNullOrWhiteSpace(extracted.MasterEntityCandidateId) &&
            masterIdMap.TryGetValue(extracted.MasterEntityCandidateId, out var matchedEntityId))
        {
            var matchedEntity = await dbContext.StructuredLearningEntities.FirstOrDefaultAsync(
                e => e.StructuredLearningEntityId == matchedEntityId, cancellationToken);
            if (matchedEntity is not null)
            {
                MergeNewAliases(matchedEntity, extracted.Aliases);
                matchedEntity.EntityClassId ??= entityClassId;
                matchedEntity.OntologicalClass ??= extracted.OntologicalClass;
                return (matchedEntity, "matched", extracted.Confidence, extracted.MatchReason);
            }
        }

        // Safety net: exact normalized-name match within the domain, even
        // if the model said "new"/"ambiguous" — never fragment an obvious
        // literal duplicate just because the model missed it. Document-
        // structural types (Article/LegalReference/...) ALSO require the
        // same TopicId, so "Artículo 1o." from a different law never merges
        // just because both share the same broader SubjectId. The
        // structural-type check can't be translated to SQL (it's a plain
        // C# helper), so candidates are materialized first, then filtered.
        StructuredLearningEntity? existing;
        if (domainId is Guid)
        {
            var candidates = await dbContext.StructuredLearningEntities
                .Where(e => e.TenantId == tenantId && e.NormalizedValue == normalized)
                .ToListAsync(cancellationToken);
            existing = candidates.FirstOrDefault(e => !IsDocumentStructuralType(e.EntityType) || e.TopicId == topicId);

            if (existing is null)
            {
                var aliasCandidates = await dbContext.StructuredLearningEntities
                    .Where(e => e.TenantId == tenantId)
                    .ToListAsync(cancellationToken);
                existing = aliasCandidates.FirstOrDefault(e =>
                    (!IsDocumentStructuralType(e.EntityType) || e.TopicId == topicId) &&
                    e.Aliases.Any(alias => Normalize(alias) == normalized));
            }
        }
        else
        {
            existing = await dbContext.StructuredLearningEntities.FirstOrDefaultAsync(
                e => e.TenantId == tenantId && e.NormalizedValue == normalized && e.EntityType == extracted.EntityType,
                cancellationToken);
        }
        if (existing is not null)
        {
            MergeNewAliases(existing, extracted.Aliases);
            existing.EntityClassId ??= entityClassId;
            existing.OntologicalClass ??= extracted.OntologicalClass;
            return (existing, "matched", 1.0, "Exact normalized name match within this domain.");
        }

        // Genuinely new or ambiguous (never auto-merged) — create a new
        // catalog row; "ambiguous" ones are flagged via ResolutionStatus on
        // the link row for later human review, not silently merged.
        var status = extracted.ResolutionStatus is "ambiguous" ? "ambiguous" : "new";
        var confidence = extracted.ResolutionStatus is "ambiguous" ? (double?)extracted.Confidence : null;

        float[]? embedding = null;
        if (_embeddingService.IsConfigured)
        {
            var embeddingText = string.Join(" | ", new[] { extracted.CanonicalName, extracted.Description }
                .Concat(extracted.Aliases)
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            embedding = await _embeddingService.GenerateEmbeddingAsync(embeddingText, cancellationToken);
        }

        var entity = new StructuredLearningEntity
        {
            StructuredLearningEntityId = Guid.NewGuid(),
            TenantId = tenantId,
            SubjectId = domainId,
            TopicId = topicId,
            Name = extracted.CanonicalName,
            EntityType = extracted.EntityType,
            EntityClassId = entityClassId,
            Description = extracted.Description,
            OntologicalClass = extracted.OntologicalClass,
            Aliases = extracted.Aliases,
            NormalizedValue = normalized,
            Embedding = embedding is null ? null : new SqlVector<float>(embedding),
            CreatedDate = DateTime.UtcNow
        };
        dbContext.StructuredLearningEntities.Add(entity);
        return (entity, status, confidence, extracted.MatchReason);
    }

    private static void MergeNewAliases(StructuredLearningEntity entity, List<string> newAliases)
    {
        var toAdd = newAliases.Where(a => !entity.Aliases.Contains(a, StringComparer.OrdinalIgnoreCase)).ToList();
        if (toAdd.Count > 0)
        {
            entity.Aliases = entity.Aliases.Concat(toAdd).ToList();
        }
    }

    private static List<ExtractedEntityDto> MergeEquivalentExtractions(List<ExtractedEntityDto> extractions)
    {
        var merged = new List<ExtractedEntityDto>();
        foreach (var extraction in extractions)
        {
            var forms = extraction.Aliases.Append(extraction.CanonicalName)
                .Where(form => !string.IsNullOrWhiteSpace(form))
                .Select(Normalize)
                .ToHashSet(StringComparer.Ordinal);
            var equivalent = merged.FirstOrDefault(existing =>
                forms.Overlaps(existing.Aliases.Append(existing.CanonicalName)
                    .Where(form => !string.IsNullOrWhiteSpace(form))
                    .Select(Normalize)));
            if (equivalent is null)
            {
                merged.Add(extraction);
                continue;
            }

            var aliasesToAdd = extraction.Aliases.Append(extraction.CanonicalName)
                .Where(alias => !string.Equals(alias, equivalent.CanonicalName, StringComparison.OrdinalIgnoreCase))
                .Where(alias => !equivalent.Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
                .ToList();
            equivalent.Aliases.AddRange(aliasesToAdd);
            if (string.IsNullOrWhiteSpace(equivalent.Description))
            {
                equivalent.Description = extraction.Description;
            }
        }

        return merged;
    }
}
