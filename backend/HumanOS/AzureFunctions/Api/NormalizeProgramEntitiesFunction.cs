using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class CleanNormalizeResultDto
{
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public int BogusEntitiesRemoved { get; set; }
    public int DuplicatesMerged { get; set; }
    public int SpecificArticlesEnsured { get; set; }
    public int TotalEntitiesAfter { get; set; }
    public int TotalOccurrencesAfter { get; set; }
    public List<string> CleanedArticleList { get; set; } = [];
}

public sealed class NormalizeProgramEntitiesFunction
{
    private readonly HumanOsDbContext _dbContext;

    public NormalizeProgramEntitiesFunction(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private static string StripDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string CanonicalKey(string text)
    {
        return string.Join(" ", StripDiacritics(text)
            .Trim()
            .ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    [Function("NormalizeProgramEntities")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programs/{programId:guid}/entities/normalize")]
        HttpRequestData request,
        Guid programId,
        CancellationToken cancellationToken)
    {
        try
        {
            var program = await _dbContext.Programs
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProgramId == programId, cancellationToken);

            if (program is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "ProgramNotFound", $"No program found with id {programId}.", cancellationToken);
            }

            // 1. Gather all StructuredLearningDocument IDs connected to this Program
            var structuredDocIds = await _dbContext.StructuredLearningDocumentProgramMemberships
                .AsNoTracking()
                .Where(m => m.ProgramId == programId)
                .Select(m => m.StructuredLearningDocumentId)
                .ToListAsync(cancellationToken);

            var capDocIds = await _dbContext.ProgramCapabilities
                .AsNoTracking()
                .Where(pc => pc.ProgramId == programId && _dbContext.StructuredLearningDocuments.Any(d => d.StructuredLearningDocumentId == pc.CapabilityId))
                .Select(pc => pc.CapabilityId)
                .ToListAsync(cancellationToken);

            var allDocIds = structuredDocIds.Concat(capDocIds).Distinct().ToList();

            if (allDocIds.Count == 0)
            {
                return await FunctionResponseFactory.SuccessResponseAsync(request, new CleanNormalizeResultDto
                {
                    ProgramId = program.ProgramId,
                    ProgramName = program.Name
                }, cancellationToken: cancellationToken);
            }

            // 2. Load all sections belonging to these courses
            var sections = await _dbContext.StructuredLearningDocumentSections
                .Include(s => s.Document)
                .Where(s => allDocIds.Contains(s.StructuredLearningDocumentId))
                .OrderBy(s => s.StructuredLearningDocumentId).ThenBy(s => s.SortOrder)
                .ToListAsync(cancellationToken);

            var tenantId = sections[0].TenantId;
            var sectionIds = sections.Select(s => s.StructuredLearningDocumentSectionId).ToList();

            // 3. Remove bogus entities:
            // A) Generic "Artículo" entity (having a generic concept named "Artículo" with 103 mentions is wrong)
            // B) Incomplete regex cut-offs like "Artículo 17 de", "Artículo 32 del", etc.
            // C) OCR visual seal/coat-of-arms artifacts ("stylized eagle", "prickly pear cactus", "serpent", etc.)
            var programEntityIds = await _dbContext.StructuredLearningEntitySections
                .Where(l => allDocIds.Contains(l.Section.StructuredLearningDocumentId))
                .Select(l => l.StructuredLearningEntityId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var allTenantEntities = await _dbContext.StructuredLearningEntities
                .Where(e => e.TenantId == tenantId || programEntityIds.Contains(e.StructuredLearningEntityId))
                .ToListAsync(cancellationToken);

            var bogusCutoffRegex = new Regex(@"^art[ií]culo\s+\d+(\s+de|\s+del)$", RegexOptions.IgnoreCase);
            var sealArtifactRegex = new Regex(@"\b(stylized\s+eagle|prickly\s+pear\s+cactus|serpent|mexican\s+national\s+coat\s+of\s+arms|intertwined\s+branches|oak\s+and\s+laurel|semicircular\s+wreath|circular\s+official\s+seal|official\s+seal|national\s+symbol|laurel|oak|wreath|águila|aguila|nopal|serpiente|guirnalda|roble)\b", RegexOptions.IgnoreCase);
            var sealTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ImageElement", "IconographicElement", "SymbolElement", "Seal", "SealLegend", "EmblemElement" };

            var bogusEntities = allTenantEntities.Where(e =>
                e.Name.Trim().Equals("Artículo", StringComparison.OrdinalIgnoreCase) ||
                e.NormalizedValue.Trim().Equals("articulo", StringComparison.OrdinalIgnoreCase) ||
                bogusCutoffRegex.IsMatch(e.Name.Trim()) ||
                sealTypes.Contains(e.EntityType) ||
                sealArtifactRegex.IsMatch(e.Name.Trim()) ||
                sealArtifactRegex.IsMatch(e.NormalizedValue ?? string.Empty) ||
                ((e.Name.Trim().Equals("Eagle", StringComparison.OrdinalIgnoreCase) || e.Name.Trim().Equals("Águila", StringComparison.OrdinalIgnoreCase) || e.Name.Trim().Equals("Aguila", StringComparison.OrdinalIgnoreCase)) && (e.EntityType == "Animal" || e.EntityType == "Symbol")) ||
                ((e.Name.Trim().Equals("Serpent", StringComparison.OrdinalIgnoreCase) || e.Name.Trim().Equals("Serpiente", StringComparison.OrdinalIgnoreCase)) && (e.EntityType == "Animal" || e.EntityType == "Symbol")) ||
                ((e.Name.Trim().Equals("Nopal", StringComparison.OrdinalIgnoreCase) || e.Name.Trim().Equals("Prickly Pear Cactus", StringComparison.OrdinalIgnoreCase)) && (e.EntityType == "Plant" || e.EntityType == "Symbol")) ||
                (e.Name.Trim().Equals("Laurel", StringComparison.OrdinalIgnoreCase) && (e.EntityType == "Plant" || e.EntityType == "Symbol")) ||
                ((e.Name.Trim().Equals("Oak", StringComparison.OrdinalIgnoreCase) || e.Name.Trim().Equals("Roble", StringComparison.OrdinalIgnoreCase)) && (e.EntityType == "Plant" || e.EntityType == "Symbol")) ||
                (e.Name.Trim().Equals("Escudo Nacional", StringComparison.OrdinalIgnoreCase) && e.EntityType == "Symbol")
            ).ToList();

            var bogusIds = bogusEntities.Select(b => b.StructuredLearningEntityId).ToHashSet();

            if (bogusIds.Count > 0)
            {
                // Find all connections that touch any of the bogus entities
                var bogusConns = await _dbContext.StructuredLearningConnections
                    .Where(c => bogusIds.Contains(c.SourceEntityId) || bogusIds.Contains(c.TargetEntityId))
                    .ToListAsync(cancellationToken);
                var bogusConnIds = bogusConns.Select(c => c.StructuredLearningConnectionId).ToHashSet();

                if (bogusConnIds.Count > 0)
                {
                    // 1. Delete dependent graph path steps first to respect FK
                    var bogusPathSteps = await _dbContext.StructuredLearningKnowledgeFactGraphPathSteps
                        .Where(s => bogusConnIds.Contains(s.ConnectionId))
                        .ToListAsync(cancellationToken);
                    if (bogusPathSteps.Count > 0)
                    {
                        _dbContext.StructuredLearningKnowledgeFactGraphPathSteps.RemoveRange(bogusPathSteps);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }

                    // 2. Delete connections
                    _dbContext.StructuredLearningConnections.RemoveRange(bogusConns);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                // 3. Delete section-entity occurrence links
                var bogusLinks = await _dbContext.StructuredLearningEntitySections
                    .Where(l => bogusIds.Contains(l.StructuredLearningEntityId))
                    .ToListAsync(cancellationToken);
                if (bogusLinks.Count > 0)
                {
                    _dbContext.StructuredLearningEntitySections.RemoveRange(bogusLinks);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                // 4. Delete the bogus entities themselves
                _dbContext.StructuredLearningEntities.RemoveRange(bogusEntities);
                await _dbContext.SaveChangesAsync(cancellationToken);

                allTenantEntities.RemoveAll(e => bogusIds.Contains(e.StructuredLearningEntityId));
            }

            // 4. Ensure a clean set of canonical LegalArticle entities exists
            // Titles match: "Artículo N.- ..."
            var titleArticleRegex = new Regex(@"^(?:art[ií]culo|art\.)\s*(\d+(?:\s*(?:bis|ter|qu[aá]ter|\w+))?)", RegexOptions.IgnoreCase);
            var inTextArticleRegex = new Regex(@"\b(?:art[ií]culo|art\.)\s+(\d+(?:\s*(?:bis|ter|qu[aá]ter))?)(?:\s+(de\s+la\s+ley|del\s+reglamento))?", RegexOptions.IgnoreCase);

            // Find or create canonical entity helper
            StructuredLearningEntity GetOrCreateArticleEntity(string canonicalName, string normalizedKey, string description)
            {
                var existing = allTenantEntities.FirstOrDefault(e =>
                    CanonicalKey(e.Name) == normalizedKey ||
                    CanonicalKey(e.NormalizedValue) == normalizedKey);

                if (existing is not null)
                {
                    existing.Name = canonicalName;
                    existing.EntityType = "LegalArticle";
                    existing.NormalizedValue = normalizedKey;
                    if (!existing.Aliases.Contains(canonicalName, StringComparer.OrdinalIgnoreCase))
                    {
                        existing.Aliases.Add(canonicalName);
                    }
                    return existing;
                }

                var newEnt = new StructuredLearningEntity
                {
                    StructuredLearningEntityId = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = canonicalName,
                    EntityType = "LegalArticle",
                    Description = description,
                    Aliases = [canonicalName, canonicalName.ToLowerInvariant()],
                    NormalizedValue = normalizedKey,
                    CreatedDate = DateTime.UtcNow
                };

                _dbContext.StructuredLearningEntities.Add(newEnt);
                allTenantEntities.Add(newEnt);
                return newEnt;
            }

            // Save any newly created entities so IDs are generated
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 5. Deduplicate and merge any existing article entities with the same canonical key
            var articleEntities = allTenantEntities
                .Where(e => e.EntityType == "LegalArticle" || Regex.IsMatch(e.Name, @"^art[ií]culo\s+\d+", RegexOptions.IgnoreCase))
                .ToList();

            var groupedArticles = articleEntities
                .GroupBy(e => CanonicalKey(e.Name))
                .ToList();

            var duplicatesMerged = 0;
            foreach (var grp in groupedArticles)
            {
                var list = grp.ToList();
                if (list.Count <= 1) continue;

                var primary = list[0];
                for (var i = 1; i < list.Count; i++)
                {
                    var dup = list[i];
                    foreach (var alias in dup.Aliases)
                    {
                        if (!primary.Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
                        {
                            primary.Aliases.Add(alias);
                        }
                    }

                    // Repoint connections
                    var sourceConns = await _dbContext.StructuredLearningConnections
                        .Where(c => c.SourceEntityId == dup.StructuredLearningEntityId)
                        .ToListAsync(cancellationToken);
                    foreach (var sc in sourceConns) sc.SourceEntityId = primary.StructuredLearningEntityId;

                    var targetConns = await _dbContext.StructuredLearningConnections
                        .Where(c => c.TargetEntityId == dup.StructuredLearningEntityId)
                        .ToListAsync(cancellationToken);
                    foreach (var tc in targetConns) tc.TargetEntityId = primary.StructuredLearningEntityId;

                    // Repoint links
                    var dupLinks = await _dbContext.StructuredLearningEntitySections
                        .Where(l => l.StructuredLearningEntityId == dup.StructuredLearningEntityId)
                        .ToListAsync(cancellationToken);

                    foreach (var link in dupLinks)
                    {
                        var alreadyLinked = await _dbContext.StructuredLearningEntitySections
                            .AnyAsync(l => l.StructuredLearningDocumentSectionId == link.StructuredLearningDocumentSectionId &&
                                           l.StructuredLearningEntityId == primary.StructuredLearningEntityId, cancellationToken);
                        if (alreadyLinked)
                        {
                            _dbContext.StructuredLearningEntitySections.Remove(link);
                        }
                        else
                        {
                            link.StructuredLearningEntityId = primary.StructuredLearningEntityId;
                        }
                    }

                    _dbContext.StructuredLearningEntities.Remove(dup);
                    allTenantEntities.Remove(dup);
                    duplicatesMerged++;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // 6. Link 100% of sections to their respective canonical article
            var existingLinks = await _dbContext.StructuredLearningEntitySections
                .Where(l => sectionIds.Contains(l.StructuredLearningDocumentSectionId))
                .ToListAsync(cancellationToken);

            var existingLinkKeys = existingLinks
                .Select(l => (l.StructuredLearningDocumentSectionId, l.StructuredLearningEntityId))
                .ToHashSet();

            var specificArticlesEnsured = 0;
            var processedArticles = new HashSet<string>();

            foreach (var section in sections)
            {
                // Title primary article
                var titleMatch = titleArticleRegex.Match(section.SubchapterTitle.Trim());
                if (titleMatch.Success)
                {
                    var num = titleMatch.Groups[1].Value.Trim();
                    // Clean number (e.g. "1", "10 Bis", "17")
                    var cleanNum = Regex.Replace(num, @"\s+", " ");
                    var canonicalName = $"Artículo {cleanNum}";
                    var key = CanonicalKey(canonicalName);

                    var entity = GetOrCreateArticleEntity(
                        canonicalName,
                        key,
                        $"Artículo {cleanNum} del {section.Document.DocumentName}.");

                    processedArticles.Add(canonicalName);

                    if (!existingLinkKeys.Contains((section.StructuredLearningDocumentSectionId, entity.StructuredLearningEntityId)))
                    {
                        var snippet = section.SubchapterTitle.Length > 80 ? section.SubchapterTitle[..80] : section.SubchapterTitle;
                        _dbContext.StructuredLearningEntitySections.Add(new StructuredLearningEntitySection
                        {
                            StructuredLearningEntitySectionId = Guid.NewGuid(),
                            StructuredLearningEntityId = entity.StructuredLearningEntityId,
                            StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                            TenantId = tenantId,
                            SourceText = snippet,
                            StartPosition = 0,
                            EndPosition = titleMatch.Length,
                            ResolutionStatus = "matched",
                            CreatedDate = DateTime.UtcNow
                        });
                        existingLinkKeys.Add((section.StructuredLearningDocumentSectionId, entity.StructuredLearningEntityId));
                        specificArticlesEnsured++;
                    }
                }

                // In-text article references
                var inTextMatches = inTextArticleRegex.Matches(section.Text);
                foreach (Match match in inTextMatches)
                {
                    var numRef = match.Groups[1].Value.Trim();
                    var lawModifier = match.Groups[2].Value.Trim();

                    string canonicalName;
                    string key;
                    if (lawModifier.Equals("de la ley", StringComparison.OrdinalIgnoreCase))
                    {
                        canonicalName = $"Artículo {numRef} de la Ley";
                        key = CanonicalKey(canonicalName);
                    }
                    else
                    {
                        canonicalName = $"Artículo {numRef}";
                        key = CanonicalKey(canonicalName);
                    }

                    var entity = GetOrCreateArticleEntity(
                        canonicalName,
                        key,
                        $"Referencia normativa al {canonicalName}.");

                    processedArticles.Add(canonicalName);

                    if (!existingLinkKeys.Contains((section.StructuredLearningDocumentSectionId, entity.StructuredLearningEntityId)))
                    {
                        var matchIdx = match.Index;
                        var start = Math.Max(0, matchIdx - 15);
                        var len = Math.Min(70, section.Text.Length - start);
                        var snippet = section.Text.Substring(start, len).Trim();

                        _dbContext.StructuredLearningEntitySections.Add(new StructuredLearningEntitySection
                        {
                            StructuredLearningEntitySectionId = Guid.NewGuid(),
                            StructuredLearningEntityId = entity.StructuredLearningEntityId,
                            StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                            TenantId = tenantId,
                            SourceText = snippet,
                            StartPosition = matchIdx,
                            EndPosition = matchIdx + match.Length,
                            ResolutionStatus = "matched",
                            CreatedDate = DateTime.UtcNow
                        });
                        existingLinkKeys.Add((section.StructuredLearningDocumentSectionId, entity.StructuredLearningEntityId));
                        specificArticlesEnsured++;
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Final Counts
            var finalOccurrences = await _dbContext.StructuredLearningEntitySections
                .CountAsync(l => sectionIds.Contains(l.StructuredLearningDocumentSectionId), cancellationToken);

            var finalEntities = await _dbContext.StructuredLearningEntitySections
                .Where(l => sectionIds.Contains(l.StructuredLearningDocumentSectionId))
                .Select(l => l.StructuredLearningEntityId)
                .Distinct()
                .CountAsync(cancellationToken);

            var result = new CleanNormalizeResultDto
            {
                ProgramId = program.ProgramId,
                ProgramName = program.Name,
                BogusEntitiesRemoved = bogusEntities.Count,
                DuplicatesMerged = duplicatesMerged,
                SpecificArticlesEnsured = specificArticlesEnsured,
                TotalEntitiesAfter = finalEntities,
                TotalOccurrencesAfter = finalOccurrences,
                CleanedArticleList = processedArticles.OrderBy(a => a).ToList()
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "NormalizationError", ex.ToString(), cancellationToken);
        }
    }
}
