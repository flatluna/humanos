using HumanOS.Agents.Studio;

namespace HumanOS.Services;

/// <summary>
/// Best-effort, deterministic (no extra LLM call) attempt to give each
/// computed "climb level" (tier) of a freshly-designed CapabilityGraph a
/// real, human title drawn from the source document's own module/chapter
/// headings (e.g. "Introducción al Marketing Digital") — 2026-08-03,
/// "levels should have a real title, not just Level 0/Level 1".
///
/// Levels are NOT a first-class concept for GraphArchitectAgent — they are
/// computed client-side (see capabilitystudio/PreviewGraphPage.tsx's
/// groupIntoLevels) as tiers of the node prerequisite DAG, and only get a
/// real name when a professor manually types one in via that page's pencil
/// icon (see <see cref="Models.Capabilities.Graph.CapabilityGraphLevel"/>).
/// This class mirrors that EXACT tiering algorithm server-side so the
/// computed LevelIndex here lines up with what the UI shows, then maps
/// each tier to whichever source chapter most of its nodes came from (via
/// each node's own <see cref="GraphNodeDto.References"/>), so the pipeline
/// can pre-populate a sensible <see cref="Models.Capabilities.Graph.CapabilityGraphLevel"/>
/// row right after creation instead of leaving every level generic.
///
/// Tiers that can't be confidently traced back to one dominant chapter
/// (e.g. no chapter data at all, or a tier whose nodes are evenly mixed
/// across several chapters) are simply absent from the result — the
/// caller leaves those to the existing "Nivel N" fallback, and the
/// professor can always name/rename any level by hand afterward.
/// </summary>
internal static class CapabilityGraphLevelAutoNamer
{
    public static List<(int LevelIndex, string Name)> ComputeLevelNames(
        List<GraphNodeDto> nodes,
        List<GraphEdgeDto> edges,
        Dictionary<string, int> chunkTagToChapterNumber,
        Dictionary<int, string> chapterTitleByNumber)
    {
        if (nodes.Count == 0 || chapterTitleByNumber.Count == 0)
        {
            return [];
        }

        // ---- 1. Assign each node to a tier — identical algorithm to
        // PreviewGraphPage.tsx's groupIntoLevels: a node's tier is 1 + the
        // deepest tier among its prerequisites (nodes with no prerequisites
        // sit at tier 0). ----
        var prerequisitesOf = nodes.ToDictionary(n => n.NodeId, _ => new List<Guid>());
        foreach (var edge in edges)
        {
            if (edge.TargetNodeId != Guid.Empty && prerequisitesOf.TryGetValue(edge.SourceNodeId, out var prereqs))
            {
                prereqs.Add(edge.TargetNodeId);
            }
        }

        var tierByNodeId = new Dictionary<Guid, int>();

        int TierOf(Guid nodeId, HashSet<Guid> visiting)
        {
            if (tierByNodeId.TryGetValue(nodeId, out var cached))
            {
                return cached;
            }

            if (!visiting.Add(nodeId))
            {
                return 0; // guards against an accidental cycle
            }

            var prereqs = prerequisitesOf.TryGetValue(nodeId, out var list) ? list : [];
            var tier = prereqs.Count == 0 ? 0 : 1 + prereqs.Max(p => TierOf(p, visiting));
            visiting.Remove(nodeId);
            tierByNodeId[nodeId] = tier;
            return tier;
        }

        foreach (var node in nodes)
        {
            TierOf(node.NodeId, []);
        }

        // ---- 2. Assign each node to its most-likely source chapter, from
        // its own References — fuzzy-matched the same way
        // NodeKnowledgeIndexService.ChunkTagMatchesReference already does
        // for RAG indexing/coverage checks, reused here for consistency
        // (GraphArchitectAgent doesn't reliably echo back the full "Cap.N
        // Label — " prefix, only some form of the original short tag). ----
        var chapterByNodeId = new Dictionary<Guid, int>();
        foreach (var node in nodes)
        {
            var votes = new Dictionary<int, int>();
            foreach (var reference in node.References)
            {
                foreach (var (tag, chapterNumber) in chunkTagToChapterNumber)
                {
                    if (NodeKnowledgeIndexService.ChunkTagMatchesReference(tag, reference))
                    {
                        votes[chapterNumber] = votes.GetValueOrDefault(chapterNumber) + 1;
                    }
                }
            }

            if (votes.Count > 0)
            {
                chapterByNodeId[node.NodeId] = votes
                    .OrderByDescending(v => v.Value)
                    .ThenBy(v => v.Key)
                    .First().Key;
            }
        }

        // ---- 3. For each tier, take the majority chapter among its nodes
        // (skip tiers where no node could be traced to any chapter). ----
        var results = new List<(int LevelIndex, string Name)>();
        foreach (var tierGroup in tierByNodeId.GroupBy(kv => kv.Value))
        {
            var chapterVotes = tierGroup
                .Select(kv => chapterByNodeId.TryGetValue(kv.Key, out var chapter) ? chapter : (int?)null)
                .Where(c => c.HasValue)
                .Select(c => c!.Value)
                .ToList();

            if (chapterVotes.Count == 0)
            {
                continue;
            }

            var dominantChapter = chapterVotes
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .First()
                .Key;

            if (chapterTitleByNumber.TryGetValue(dominantChapter, out var title) && !string.IsNullOrWhiteSpace(title))
            {
                results.Add((tierGroup.Key, title.Trim()));
            }
        }

        return results;
    }
}
