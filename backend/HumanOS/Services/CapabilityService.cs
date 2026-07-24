using HumanOS.Contracts.Capabilities;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class CapabilityService
{
    private readonly HumanOsDbContext _dbContext;

    public CapabilityService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CapabilityResponse>> GetActiveAsync(
        string languageCode,
        string? domainCode = null,
        string? subjectCode = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = languageCode.Trim();
        var query = _dbContext.Capabilities
            .AsNoTracking()
            .Include(c => c.CapabilityDomain)
            .Include(c => c.Subject)
            .Include(c => c.Translations)
            .Include(c => c.Levels)
                .ThenInclude(l => l.Modules)
            .Include(c => c.CapabilityGraph)
                .ThenInclude(g => g!.Nodes)
            .Include(c => c.CapabilityGraph)
                .ThenInclude(g => g!.Edges)
            .Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(domainCode))
        {
            var normalizedDomainCode = domainCode.Trim().ToUpperInvariant();
            query = query.Where(c => c.CapabilityDomain.Code == normalizedDomainCode);
        }

        if (!string.IsNullOrWhiteSpace(subjectCode))
        {
            var normalizedSubjectCode = subjectCode.Trim().ToLowerInvariant();
            query = query.Where(c => c.Subject != null && c.Subject.Code == normalizedSubjectCode);
        }

        var capabilities = await query
            .OrderBy(c => c.CapabilityDomain.Code)
            .ThenBy(c => c.Code)
            .ToListAsync(cancellationToken);

        return capabilities.Select(c => new CapabilityResponse
        {
            CapabilityId = c.CapabilityId,
            CapabilityDomainId = c.CapabilityDomainId,
            DomainCode = c.CapabilityDomain.Code,
            SubjectId = c.SubjectId,
            SubjectCode = c.Subject?.Code,
            Code = c.Code,
            Name = c.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Name
                ?? c.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? c.Name,
            Description = c.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Description
                ?? c.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Description
                ?? c.Description,
            IsActive = c.IsActive,
            // V1 capabilities (legacy course-authoring flow) have real
            // CapabilityLevel rows; V2 capabilities (the PDF/idea graph
            // pipeline — everything created today) have NONE, so this used
            // to always read 0 for every capability that actually exists.
            // Fall back to a tier count computed from the graph's own
            // prerequisite edges (same "climb levels" grouping
            // PreviewGraphPage.tsx shows) so the stat means something for
            // graph-based capabilities too.
            LevelCount = c.Levels.Count > 0
                ? c.Levels.Count
                : ComputeGraphLevelCount(c.CapabilityGraph),
            ModuleCount = c.Levels.Sum(l => l.Modules.Count),
            NodeCount = c.CapabilityGraph?.Nodes.Count ?? 0,
            HasCoverImage = !string.IsNullOrEmpty(c.CapabilityGraph?.CoverImageStoragePath),
            LearningSummary = Truncate(c.CapabilityGraph?.ExecutiveSummary, 95),
            Levels = [.. c.Levels.OrderBy(l => l.SortOrder).Select(l => l.Layer)],
            CreatedDate = c.CreatedDate,
            UpdatedDate = c.UpdatedDate
        }).ToList();
    }

    /// <summary>
    /// Number of "climb levels" in a V2 capability graph — nodes with no
    /// prerequisite edges form level 0, and each subsequent level is
    /// 1 + the deepest prerequisite's level (mirrors groupIntoLevels in
    /// capabilitystudio/src/pages/PreviewGraphPage.tsx exactly, so the
    /// catalog's "Niveles" stat matches what the graph preview actually
    /// shows). Returns 0 when there's no graph yet or it has no nodes.
    /// </summary>
    private static int ComputeGraphLevelCount(HumanOS.Models.Capabilities.Graph.CapabilityGraph? graph)
    {
        if (graph is null || graph.Nodes.Count == 0)
        {
            return 0;
        }

        var prerequisitesOf = graph.Nodes.ToDictionary(
            n => n.CapabilityGraphNodeId,
            _ => new List<Guid>());

        foreach (var edge in graph.Edges)
        {
            if (prerequisitesOf.TryGetValue(edge.SourceNodeId, out var prereqs))
            {
                prereqs.Add(edge.TargetNodeId);
            }
        }

        var tierCache = new Dictionary<Guid, int>();

        int TierOf(Guid nodeId, HashSet<Guid> visiting)
        {
            if (tierCache.TryGetValue(nodeId, out var cached))
            {
                return cached;
            }

            // Guards against an accidental cycle, same as the frontend.
            if (!visiting.Add(nodeId))
            {
                return 0;
            }

            var prereqs = prerequisitesOf.TryGetValue(nodeId, out var p) ? p : [];
            var tier = prereqs.Count == 0 ? 0 : 1 + prereqs.Max(prereqId => TierOf(prereqId, visiting));

            visiting.Remove(nodeId);
            tierCache[nodeId] = tier;
            return tier;
        }

        var maxTier = 0;
        foreach (var node in graph.Nodes)
        {
            maxTier = Math.Max(maxTier, TierOf(node.CapabilityGraphNodeId, []));
        }

        return maxTier + 1;
    }

    /// <summary>Trims a long free-text summary down to a short card-friendly
    /// teaser, cutting at the nearest word boundary and appending "…".</summary>
    private static string? Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        var cut = trimmed.LastIndexOf(' ', maxLength);
        if (cut <= 0)
        {
            cut = maxLength;
        }

        return trimmed[..cut].TrimEnd('.', ',', ' ') + "…";
    }

    public async Task<CapabilityResponse?> GetByCodeAsync(
        string code,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var normalizedLanguageCode = languageCode.Trim();

        var capability = await _dbContext.Capabilities
            .AsNoTracking()
            .Include(c => c.CapabilityDomain)
            .Include(c => c.Translations)
            .SingleOrDefaultAsync(
                c => c.Code == normalizedCode,
                cancellationToken);

        if (capability is null)
        {
            return null;
        }

        return new CapabilityResponse
        {
            CapabilityId = capability.CapabilityId,
            CapabilityDomainId = capability.CapabilityDomainId,
            DomainCode = capability.CapabilityDomain.Code,
            Code = capability.Code,
            Name = capability.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Name
                ?? capability.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? capability.Name,
            Description = capability.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Description
                ?? capability.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Description
                ?? capability.Description,
            IsActive = capability.IsActive
        };
    }

    public async Task<CapabilityResponse?> GetByIdAsync(
        Guid capabilityId,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = languageCode.Trim();

        var capability = await _dbContext.Capabilities
            .AsNoTracking()
            .Include(c => c.CapabilityDomain)
            .Include(c => c.Translations)
            .SingleOrDefaultAsync(
                c => c.CapabilityId == capabilityId,
                cancellationToken);

        if (capability is null)
        {
            return null;
        }

        return new CapabilityResponse
        {
            CapabilityId = capability.CapabilityId,
            CapabilityDomainId = capability.CapabilityDomainId,
            DomainCode = capability.CapabilityDomain.Code,
            Code = capability.Code,
            Name = capability.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Name
                ?? capability.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                ?? capability.Name,
            Description = capability.Translations?.FirstOrDefault(t => t.LanguageCode == normalizedLanguageCode)?.Description
                ?? capability.Translations?.FirstOrDefault(t => t.LanguageCode == "en")?.Description
                ?? capability.Description,
            IsActive = capability.IsActive
        };
    }

    /// <summary>
    /// Full read-only content (levels + modules + scripts + metrics) for
    /// the "view real generated content" screen. Unlike GetByIdAsync,
    /// this does NOT apply translations (content is authored in a single
    /// language today) and includes the full per-module Script text.
    /// </summary>
    public async Task<CapabilityContentResponse?> GetContentByIdAsync(
        Guid capabilityId,
        CancellationToken cancellationToken = default)
    {
        var capability = await _dbContext.Capabilities
            .AsNoTracking()
            .Include(c => c.Levels)
                .ThenInclude(l => l.Modules)
                    .ThenInclude(m => m.Metrics)
            .SingleOrDefaultAsync(c => c.CapabilityId == capabilityId, cancellationToken);

        if (capability is null)
        {
            return null;
        }

        return new CapabilityContentResponse
        {
            CapabilityId = capability.CapabilityId,
            Code = capability.Code,
            Name = capability.Name,
            Description = capability.Description,
            Levels = [.. capability.Levels
                .OrderBy(l => l.SortOrder)
                .Select(l => new CapabilityContentLevel
                {
                    CapabilityLevelId = l.CapabilityLevelId,
                    Layer = l.Layer,
                    Title = l.Title,
                    HumanTransformation = l.HumanTransformation,
                    Modules = [.. l.Modules
                        .OrderBy(m => m.SortOrder)
                        .Select(m => new CapabilityContentModule
                        {
                            CapabilityModuleId = m.CapabilityModuleId,
                            SortOrder = m.SortOrder,
                            Title = m.Title,
                            Description = m.Description,
                            Type = m.Type,
                            Script = m.Script,
                            MetricRationale = m.MetricRationale,
                            Metrics = [.. m.Metrics.Select(mm => mm.Metric)]
                        })]
                })]
        };
    }

    /// <summary>
    /// Read-only chapter list for a single module (added 2026-07-16) — lets
    /// the Runtime frontend show a previously-seen chapter's raw teaching
    /// content again ("review mode") without starting a new Runtime turn
    /// or involving the Tutor Agent.
    /// </summary>
    public async Task<ModuleChapterSummaryResponse?> GetModuleChaptersAsync(
        Guid capabilityModuleId,
        CancellationToken cancellationToken = default)
    {
        var module = await _dbContext.CapabilityModules
            .AsNoTracking()
            .Include(m => m.Chapters)
            .SingleOrDefaultAsync(m => m.CapabilityModuleId == capabilityModuleId, cancellationToken);

        if (module is null)
        {
            return null;
        }

        return new ModuleChapterSummaryResponse
        {
            CapabilityModuleId = module.CapabilityModuleId,
            ModuleTitle = module.Title,
            Chapters = [.. module.Chapters
                .OrderBy(c => c.SortOrder)
                .Select(c => new ModuleChapterSummary
                {
                    CapabilityModuleChapterId = c.CapabilityModuleChapterId,
                    SortOrder = c.SortOrder,
                    Title = c.Title,
                    TeachingContent = c.TeachingContent,
                    IsPrimaryWeight = c.IsPrimaryWeight
                })]
        };
    }

    /// <summary>
    /// Permanently deletes a capability and EVERYTHING that hangs off it —
    /// levels/modules (V1 course authoring), the CapabilityGraph/nodes/
    /// blueprints/illustrations (V2), any Learning Runtime sessions run
    /// against it, and any learner progress (PersonCapability, Evidence,
    /// GrowthActions, GoalCapability links, Assessments). This is
    /// deliberately irreversible and destroys real learner history if any
    /// student has already engaged with this capability — the caller is
    /// expected to have shown a clear, explicit warning before invoking
    /// this (see DeleteCapabilityFunction / capabilitystudio's delete
    /// confirmation modal).
    ///
    /// Wrapped in a single DB transaction so the whole operation is ACID:
    /// either every one of these tables loses its rows for this capability
    /// AND the Capability row itself is gone, or (on any failure) NOTHING
    /// is deleted — no partial/orphaned state is possible. Runs each step
    /// as a set-based <c>ExecuteDeleteAsync</c> (translated to a single SQL
    /// DELETE per step) rather than loading+removing entities, both for
    /// performance and so the whole thing stays inside one transaction
    /// without needing to materialize potentially large graphs into memory.
    ///
    /// Deletion order matters: most children cascade at the DB level once
    /// their direct parent is deleted (e.g. CapabilityLevel -> Module,
    /// CapabilityGraph -> Node -> Edge/Illustration/Blueprint), but several
    /// FKs are deliberately Restrict/NoAction instead of Cascade (to avoid
    /// SQL Server's "multiple cascade paths" error on tables with more than
    /// one route back to Capability) and would otherwise make the final
    /// Capability delete fail with a FK constraint violation. Those rows
    /// must be deleted explicitly, in dependency order (deepest children
    /// first), before the tables they Restrict-reference:
    ///   1. LearningSessions (CapabilityId Restrict) — DB-cascades down to
    ///      LearningSessionNodes/Steps/Evidence/AssessmentResults/
    ///      AssessmentRounds/AssessmentQuestions. Must happen before step 2,
    ///      since LearningSessionNode.CapabilityGraphNodeId/
    ///      NodeExperienceBlueprintId are Restrict against the graph nodes
    ///      step 8 (via the final Capability delete) will cascade-delete.
    ///   2. CapabilityGraphNodeKnowledgeExpansions for this capability's
    ///      nodes — Expansion.DiagramIllustrationId is Restrict against the
    ///      node's own illustration, which would otherwise conflict with
    ///      the Illustration cascade fired by the final Capability delete.
    ///   3. AgentMessages, then Agents (CapabilityId Restrict).
    ///   4. AssessmentAttempts, then Assessments (CapabilityId Restrict).
    ///   5. GrowthActions referencing this capability's PersonCapabilities
    ///      (PersonCapabilityId Restrict).
    ///   6. CapabilityEvidence referencing this capability's Evidence rows
    ///      or PersonCapabilities (both FKs Restrict).
    ///   7. Evidence rows for this capability (CapabilityId Restrict).
    ///   8. PersonCapabilities for this capability (CapabilityId Restrict) —
    ///      this is real learner progress/mastery data.
    ///   9. GoalCapability links (CapabilityId Restrict).
    ///  10. CapabilityKnowledgeChunks (V1 RAG chunks, CapabilityId Restrict).
    ///  11. ProgramCapability links (CapabilityId Restrict) — added later
    ///      alongside the Program tables (2026-07-23) and originally missed
    ///      here, which caused DELETEs of any capability attached to a
    ///      Program to fail with an FK violation (surfaced as a 500).
    ///  12. The Capability row itself — cascades at the DB level to
    ///      CapabilityTranslations, CapabilityLevels (-> Modules -> Metrics/
    ///      Verifications/Chapters), CapabilityGraph (-> Nodes -> Edges/
    ///      Illustrations/KnowledgeChunks/Blueprints/Validations), and
    ///      CapabilityGenerationUsage.
    /// Returns false if no matching capability was found (transaction is
    /// still opened but nothing is deleted/committed in that case either
    /// way — cheap no-op).
    /// </summary>
    public async Task<bool> DeleteAsync(Guid capabilityId, CancellationToken cancellationToken = default)
    {
        var capabilityExists = await _dbContext.Capabilities
            .AsNoTracking()
            .AnyAsync(c => c.CapabilityId == capabilityId, cancellationToken);

        if (!capabilityExists)
        {
            return false;
        }

        // SqlServerRetryingExecutionStrategy (EnableRetryOnFailure) refuses
        // to let you open a user-managed transaction directly — retries
        // would silently re-run partially-committed work. EF Core's fix is
        // to have the *strategy itself* own and retry the whole
        // transaction as one atomic unit via ExecuteAsync.
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        var rowsDeleted = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. Learning Runtime (V2) sessions — DB-cascades down to nodes/
                // steps/evidence/assessment rounds & questions.
                await _dbContext.LearningSessions
                    .Where(s => s.CapabilityId == capabilityId)
                    .ExecuteDeleteAsync(cancellationToken);

                // 2. Per-node knowledge-expansion cache ("Profundizar").
                await _dbContext.CapabilityGraphNodeKnowledgeExpansions
                    .Where(e => e.CapabilityGraphNode.CapabilityGraph.CapabilityId == capabilityId)
                    .ExecuteDeleteAsync(cancellationToken);

                // 3. Agents / AgentMessages tied to this capability.
                await _dbContext.AgentMessages
                    .Where(m => m.Agent.CapabilityId == capabilityId)
                    .ExecuteDeleteAsync(cancellationToken);

                await _dbContext.Agents
                    .Where(a => a.CapabilityId == capabilityId)
                    .ExecuteDeleteAsync(cancellationToken);

                // 4. Assessments (V1) / attempts tied to this capability.
                await _dbContext.AssessmentAttempts
                    .Where(a => a.Assessment.CapabilityId == capabilityId)
                    .ExecuteDeleteAsync(cancellationToken);

                await _dbContext.Assessments
                    .Where(a => a.CapabilityId == capabilityId)
                    .ExecuteDeleteAsync(cancellationToken);

                // 5-8. Learner progress: GrowthActions -> CapabilityEvidence ->
                // Evidence -> PersonCapabilities, in that dependency order.
                await _dbContext.GrowthActions
                    .Where(g => g.PersonCapability.CapabilityId == capabilityId)
                    .ExecuteDeleteAsync(cancellationToken);

                await _dbContext.CapabilityEvidence
                    .Where(e => e.PersonCapability.CapabilityId == capabilityId || e.Evidence.CapabilityId == capabilityId)
                    .ExecuteDeleteAsync(cancellationToken);

                await _dbContext.Evidence
                    .Where(e => e.CapabilityId == capabilityId)
                    .ExecuteDeleteAsync(cancellationToken);

                await _dbContext.PersonCapabilities
                    .Where(p => p.CapabilityId == capabilityId)
                    .ExecuteDeleteAsync(cancellationToken);

                // 9. Goal <-> Capability links.
                await _dbContext.GoalCapabilities
                    .Where(g => g.CapabilityId == capabilityId)
                    .ExecuteDeleteAsync(cancellationToken);

                // 10. V1 RAG knowledge chunks.
                await _dbContext.CapabilityKnowledgeChunks
                    .Where(k => k.CapabilityId == capabilityId)
                    .ExecuteDeleteAsync(cancellationToken);

                // 11. Program <-> Capability links (Restrict FK, added
                // later with the Program tables — see doc comment above).
                await _dbContext.ProgramCapabilities
                    .Where(p => p.CapabilityId == capabilityId)
                    .ExecuteDeleteAsync(cancellationToken);

                // 12. The Capability itself — cascades to Translations, Levels
                // (-> Modules -> Metrics/Verifications/Chapters), and
                // CapabilityGraph (-> Nodes -> Edges/Illustrations/
                // KnowledgeChunks/Blueprints/Validations).
                var deleted = await _dbContext.Capabilities
                    .Where(c => c.CapabilityId == capabilityId)
                    .ExecuteDeleteAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return deleted;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        return rowsDeleted > 0;
    }
}
