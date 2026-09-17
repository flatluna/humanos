using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HumanOS.Services;

/// <summary>
/// Knowledge Graph edges (2026-08-14) — extracts relationships between
/// entities ALREADY linked to one section (via
/// <see cref="StructuredLearningEntitySection"/>) and persists them as
/// <see cref="StructuredLearningConnection"/> rows. Must run AFTER
/// <see cref="StructuredLearningEntityExtractionService.ExtractForSectionAsync"/>
/// for the same section — it only ever offers the LLM that section's own
/// already-extracted entity names as a closed candidate list, so every
/// edge is guaranteed to resolve to a real entity row (never a dangling
/// reference).
///
/// Idempotent by design, same discipline as entity extraction: re-running
/// for the same section never creates a duplicate edge (looked up by
/// TenantId+SourceEntityId+TargetEntityId+RelationshipLabel before
/// inserting).
/// </summary>
public sealed class StructuredLearningConnectionExtractionService
{
    private readonly StructuredLearningConnectionExtractionAgent _extractionAgent;
    private readonly StructuredLearningGenerationUsageService _usageService;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;

    public StructuredLearningConnectionExtractionService(
        StructuredLearningConnectionExtractionAgent extractionAgent,
        StructuredLearningGenerationUsageService usageService,
        IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _extractionAgent = extractionAgent;
        _usageService = usageService;
        _dbContextFactory = dbContextFactory;
    }

    public bool IsConfigured => _extractionAgent.IsConfigured;

    /// <summary>Extracts and persists connections for ONE section, using
    /// only the entities already linked to it. REPLACES (2026-08-17, fixes
    /// duplicate/near-duplicate edge buildup on repeated "Regenerar Grafo"
    /// clicks — the exact-label dedup key used further below cannot catch
    /// two runs that reword the same real-world relationship slightly
    /// differently) rather than only adds: every existing connection
    /// sourced from this section, and every Knowledge Fact derived from
    /// them (which would otherwise dangle/FK-block the delete), is removed
    /// first, then the fresh extraction is inserted from scratch. Facts are
    /// safe to drop here because the only caller ("Regenerar Grafo" in
    /// KnowledgeGraphSection.tsx) always re-generates Facts immediately
    /// after calling this. Returns the number of connections created.</summary>
    public async Task<int> ExtractForSectionAsync(Guid sectionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var section = await dbContext.StructuredLearningDocumentSections
            .Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new InvalidOperationException($"No se encontró la sección '{sectionId}'.");

        var staleFacts = await dbContext.StructuredLearningKnowledgeFacts
            .Where(fact => fact.EngramId == sectionId)
            .ToListAsync(cancellationToken);
        dbContext.StructuredLearningKnowledgeFacts.RemoveRange(staleFacts);

        var staleConnections = await dbContext.StructuredLearningConnections
            .Where(c => c.SourceSectionId == sectionId)
            .ToListAsync(cancellationToken);
        dbContext.StructuredLearningConnections.RemoveRange(staleConnections);
        await dbContext.SaveChangesAsync(cancellationToken);

        var candidateEntities = await dbContext.StructuredLearningEntitySections
            .Where(l => l.StructuredLearningDocumentSectionId == sectionId)
            .Select(l => new { l.StructuredLearningEntityId, l.Entity.Name })
            .Distinct()
            .ToListAsync(cancellationToken);

        // Fewer than 2 candidate entities means no possible edge — skip the
        // LLM call entirely (nothing meaningful to relate).
        if (candidateEntities.Count < 2)
        {
            return 0;
        }

        var nameToEntityId = candidateEntities
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().StructuredLearningEntityId, StringComparer.OrdinalIgnoreCase);

        // GraphRAG-style short Ids (2026-08-15) — WE generate these deterministically
        // (never the model), so a candidate's Id->entity mapping is always reliable
        // regardless of model behavior; the model only ever has to echo back a short
        // "e3"-style token instead of repeating the full entity name on every edge,
        // cutting output tokens and eliminating "name didn't match exactly" failures.
        var distinctNames = candidateEntities.Select(e => e.Name).Distinct().ToList();
        var idToEntityId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var candidateIdNamePairs = new List<(string Id, string Name)>();
        for (var i = 0; i < distinctNames.Count; i++)
        {
            var id = $"e{i}";
            idToEntityId[id] = nameToEntityId[distinctNames[i]];
            candidateIdNamePairs.Add((id, distinctNames[i]));
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var languageCode = ResolveLanguage(section.Document?.ConfigSnapshotJson, section.ConfigSnapshotJson, section.Text);
        var extractionResult = await _extractionAgent.ExtractAsync(
            section.SubchapterTitle, section.Text, candidateIdNamePairs, existingRelationshipLabels: [], languageCode, cancellationToken);
        stopwatch.Stop();

        // TEMP DEBUG (2026-08-15)
        var kgDebugLines = new List<string>
        {
            $"[KG-DEBUG] section={sectionId} candidates={candidateEntities.Count} candidateNames=[{string.Join(", ", candidateEntities.Select(e => e.Name))}] rawConnectionCount={extractionResult.Response.Connections.Count} outputTokens={extractionResult.OutputTokens} textLength={section.Text?.Length ?? 0}"
        };
        if (extractionResult.Response.Connections.Count == 0)
        {
            kgDebugLines.Add($"[KG-DEBUG]   ZERO CONNECTIONS! Title='{section.SubchapterTitle}' Text='{section.Text?.Substring(0, Math.Min(100, section.Text?.Length ?? 0))}'");
        }
        foreach (var raw in extractionResult.Response.Connections)
        {
            kgDebugLines.Add($"[KG-DEBUG]   {raw.SourceEntityId} --{raw.RelationshipLabel}--> {raw.TargetEntityId} ({raw.RelationshipCategory}) evidence=\"{raw.Evidence}\"");
        }
        try
        {
            System.IO.File.AppendAllLines(@"C:\Temp\kg-debug.log", kgDebugLines);
        }
        catch { /* debug logging must never break extraction */ }

        try
        {
            await _usageService.RecordAsync(
                section.StructuredLearningDocumentId, sectionId, "KnowledgeGraph", "StructuredLearningConnectionExtractionAgent",
                extractionResult.ModelName, extractionResult.InputTokens, extractionResult.OutputTokens,
                extractionResult.CachedInputTokens, stopwatch.ElapsedMilliseconds, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Usage logging must never break the actual extraction it measures.
        }

        var extraction = extractionResult.Response;
        if (extraction.Connections.Count == 0)
        {
            return 0;
        }

        var created = 0;
        var responseKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var connection in extraction.Connections)
        {
            if (string.IsNullOrWhiteSpace(connection.RelationshipLabel) ||
                !idToEntityId.TryGetValue(connection.SourceEntityId, out var sourceEntityId) ||
                !idToEntityId.TryGetValue(connection.TargetEntityId, out var targetEntityId) ||
                sourceEntityId == targetEntityId)
            {
                // Skip anything that doesn't resolve to two distinct real
                // entities from this section's own candidate list — never
                // persist a hallucinated/self-referencing edge.
                continue;
            }

            var category = StructuredLearningConnectionExtractionAgent.NormalizeRelationshipCategory(connection.RelationshipCategory);
            if (!IsCompatibleCategory(category, connection.RelationshipLabel))
            {
                category = "RelatedTo";
            }

            var responseKey = $"{sourceEntityId}|{targetEntityId}|{connection.RelationshipLabel.Trim()}";
            if (!responseKeys.Add(responseKey))
            {
                continue;
            }

            // Scoped to THIS document (not just TenantId) — entities can be shared/
            // reused canonically across multiple documents in the same tenant, so a
            // tenant-wide check previously caused false-positive duplicates that
            // silently blocked this document's own edge from ever being created.
            var alreadyExists = await dbContext.StructuredLearningConnections.AnyAsync(
                c => c.StructuredLearningDocumentId == section.StructuredLearningDocumentId &&
                     c.SourceEntityId == sourceEntityId &&
                     c.TargetEntityId == targetEntityId &&
                     c.RelationshipLabel == connection.RelationshipLabel,
                cancellationToken);
            if (alreadyExists)
            {
                continue;
            }

            dbContext.StructuredLearningConnections.Add(new StructuredLearningConnection
            {
                StructuredLearningConnectionId = Guid.NewGuid(),
                StructuredLearningDocumentId = section.StructuredLearningDocumentId,
                TenantId = section.TenantId,
                SourceEntityId = sourceEntityId,
                RelationshipLabel = connection.RelationshipLabel,
                RelationshipCategory = category,
                TargetEntityId = targetEntityId,
                SourceSectionId = sectionId,
                Evidence = string.IsNullOrWhiteSpace(connection.Evidence) ? null : connection.Evidence,
                EpistemicStatus = connection.EpistemicStatus is "explicit" or "inferred" or "referenced" or "attributed" or "hypothetical"
                    ? connection.EpistemicStatus
                    : "explicit",
                Confidence = connection.Confidence,
                CreatedDate = DateTime.UtcNow
            });
            created++;
        }

        // Root is ALWAYS the Engram's own title entity (2026-08-15, user
        // requirement) — never the model's free "conceptual hub" choice.
        // e.g. section title "Artículo 1o." -> root is the "Artículo 1o."
        // entity, even though the model might consider e.g. "Impuesto al
        // valor agregado" more central. Matched by containment either way
        // (title contains the entity name, or vice versa), case-insensitive.
        // Falls back to the model's RootEntityId only if no candidate
        // entity's name relates to the section's own title at all.
        var titleRootPair = candidateIdNamePairs.FirstOrDefault(e =>
            section.SubchapterTitle.Contains(e.Name, StringComparison.OrdinalIgnoreCase) ||
            e.Name.Contains(section.SubchapterTitle, StringComparison.OrdinalIgnoreCase));
        if (titleRootPair.Name is not null)
        {
            section.KnowledgeGraphRootEntityName = titleRootPair.Name;
        }
        else if (!string.IsNullOrWhiteSpace(extraction.RootEntityId) &&
            candidateIdNamePairs.FirstOrDefault(e => e.Id == extraction.RootEntityId) is { Name: not null } rootPair)
        {
            section.KnowledgeGraphRootEntityName = rootPair.Name;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }

    /// <summary>Reads the document's or section's own configured LanguageCode,
    /// falling back to a text-based language detector. Ensures Spanish documents
    /// get Spanish relationship labels.</summary>
    private static string ResolveLanguage(string? documentConfigJson, string? sectionConfigJson, string? sampleText)
    {
        foreach (var configJson in new[] { sectionConfigJson, documentConfigJson })
        {
            if (string.IsNullOrWhiteSpace(configJson)) continue;
            try
            {
                var snapshot = JsonSerializer.Deserialize<JsonElement>(configJson);
                foreach (var prop in snapshot.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "languageCode", StringComparison.OrdinalIgnoreCase) &&
                        prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var code = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            return code.Trim().ToLowerInvariant().StartsWith("es") ? "es" : "en";
                        }
                    }
                }
            }
            catch (JsonException) { }
        }

        if (!string.IsNullOrWhiteSpace(sampleText))
        {
            var padded = $" {sampleText.ToLowerInvariant()} ";
            string[] englishWords = [" the ", " and ", " you ", " is ", " to ", " your ", " with ", " that ", " this "];
            string[] spanishWords = [" el ", " la ", " de ", " que ", " los ", " las ", " para ", " con ", " es ", " tu ", " artículo ", " articulo ", " ley ", " uif ", " sat "];

            var englishHits = englishWords.Count(w => padded.Contains(w));
            var spanishHits = spanishWords.Count(w => padded.Contains(w));

            return spanishHits >= englishHits ? "es" : "en";
        }

        return "es";
    }

    private static bool IsCompatibleCategory(string category, string label)
    {
        var normalized = label.Trim().ToLowerInvariant();
        return category switch
        {
            "PartOf" => normalized.Contains("part of") || normalized.Contains("componente") || normalized.Contains("parte de") || normalized.Contains("belongs to") || normalized.Contains("pertenece a"),
            "IsA" => normalized.Contains("is a") || normalized.Contains("is an") || normalized.Contains("subset") || normalized.Contains("capability") || normalized.Contains("tipo") || normalized.Contains("categoría") || normalized.Contains("category"),
            "Processes" => normalized.Contains("analyz") || normalized.Contains("process") || normalized.Contains("transform") || normalized.Contains("acts on") || normalized.Contains("analiza") || normalized.Contains("procesa") || normalized.Contains("transforma"),
            _ => true
        };
    }
}
