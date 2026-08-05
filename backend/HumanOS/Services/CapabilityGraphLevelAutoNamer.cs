using HumanOS.Agents.Studio;

namespace HumanOS.Services;

/// <summary>
/// Best-effort, deterministic (no extra LLM call) attempt to group each
/// freshly-designed CapabilityGraph's nodes into "climb levels" that match
/// the SOURCE DOCUMENT's own chapter/module structure (e.g. all of chapter
/// "1. Introducción"'s nodes sit together in one level, however many there
/// are), and give each level a real title drawn from that chapter's own
/// heading — 2026-08-04, replacing an earlier version of this class that
/// grouped nodes by the graph's prerequisite-edge DEPTH instead. That
/// edge-depth approach put sibling nodes from the very same chapter into
/// DIFFERENT rows whenever one had a "Requires" edge onto the other (a
/// common, legitimate authoring choice for e.g. "purpose" before
/// "importance" within the same intro chapter) — both rows would then be
/// auto-named identically after that one shared chapter, which visually
/// read as a duplicated/broken level. Grouping by chapter instead means a
/// level's name is 1:1 chapter title, so this scenario simply can't recur.
///
/// Levels are NOT a first-class concept for GraphArchitectAgent — this
/// class computes each node's chapter (via its own
/// <see cref="GraphNodeDto.References"/>, fuzzy-matched against the
/// document's own chunk tags) and persists the result on
/// <see cref="Models.Capabilities.Graph.CapabilityGraphNode.ChapterIndex"/>
/// right after graph creation, which is what
/// capabilitystudio/PreviewGraphPage.tsx's groupIntoLevels groups nodes by
/// (falling back to the old edge-depth tiering only for graphs created
/// before this field existed, where every node's ChapterIndex is null).
/// </summary>
internal static class CapabilityGraphLevelAutoNamer
{
    public sealed class Result
    {
        /// <summary>One entry per distinct chapter that could be traced,
        /// in document order — LevelIndex is 0-based and sequential.</summary>
        public List<(int LevelIndex, string Name)> Levels { get; init; } = [];

        /// <summary>Every node whose source chapter could be determined
        /// (directly or inherited from a neighboring node — see below),
        /// mapped to the SAME 0-based LevelIndex used in <see cref="Levels"/>.
        /// Nodes absent from this map (should be rare) get a null
        /// ChapterIndex and fall back to the old edge-tier grouping.</summary>
        public Dictionary<Guid, int> LevelIndexByNodeId { get; init; } = [];
    }

    public static Result ComputeLevelNames(
        List<GraphNodeDto> nodes,
        Dictionary<string, int> chunkTagToChapterNumber,
        Dictionary<int, string> chapterTitleByNumber)
    {
        if (nodes.Count == 0 || chapterTitleByNumber.Count == 0)
        {
            return new Result();
        }

        // ---- 1. Assign each node to its most-likely source chapter, from
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

        // ---- 2. Nodes that couldn't be traced to any chapter (rare — only
        // ever expected for manually-added nodes with no References)
        // inherit whichever chapter the nearest PRECEDING node (by
        // SortOrder, the document's own reading order) belongs to, so they
        // still land in a sensible level instead of getting dropped. Any
        // still-untraced LEADING nodes fall back to the first known
        // chapter instead. ----
        var orderedNodes = nodes.OrderBy(n => n.SortOrder).ToList();
        int? lastKnownChapter = null;
        foreach (var node in orderedNodes)
        {
            if (chapterByNodeId.TryGetValue(node.NodeId, out var chapter))
            {
                lastKnownChapter = chapter;
            }
            else if (lastKnownChapter is int fallbackChapter)
            {
                chapterByNodeId[node.NodeId] = fallbackChapter;
            }
        }

        var firstKnownChapter = orderedNodes
            .Select(n => chapterByNodeId.TryGetValue(n.NodeId, out var c) ? c : (int?)null)
            .FirstOrDefault(c => c.HasValue);
        if (firstKnownChapter.HasValue)
        {
            foreach (var node in orderedNodes.Where(n => !chapterByNodeId.ContainsKey(n.NodeId)))
            {
                chapterByNodeId[node.NodeId] = firstKnownChapter.Value;
            }
        }

        // ---- 3. Group nodes by their assigned chapter, in the order that
        // chapter first appears in the document's own node ordering — this
        // becomes each chapter's sequential 0-based LevelIndex. ----
        var chapterOrder = orderedNodes
            .Select(n => chapterByNodeId.TryGetValue(n.NodeId, out var c) ? c : (int?)null)
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .Distinct()
            .ToList();

        var levelIndexByChapter = chapterOrder
            .Select((chapter, index) => (chapter, index))
            .ToDictionary(x => x.chapter, x => x.index);

        var levelIndexByNodeId = chapterByNodeId
            .Where(kv => levelIndexByChapter.ContainsKey(kv.Value))
            .ToDictionary(kv => kv.Key, kv => levelIndexByChapter[kv.Value]);

        var levels = new List<(int LevelIndex, string Name)>();
        foreach (var chapter in chapterOrder)
        {
            if (chapterTitleByNumber.TryGetValue(chapter, out var title) && !string.IsNullOrWhiteSpace(title))
            {
                levels.Add((levelIndexByChapter[chapter], title.Trim()));
            }
        }

        return new Result
        {
            Levels = levels,
            LevelIndexByNodeId = levelIndexByNodeId
        };
    }
}

