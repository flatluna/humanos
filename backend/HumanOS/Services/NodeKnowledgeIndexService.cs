using System.Text.Json;
using HumanOS.Agentic.Studio;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// One chunk retrieved via <see cref="NodeKnowledgeIndexService.SearchAsync"/> —
/// which node it came from (for the Tutor to optionally attribute, e.g.
/// "eso lo vimos en...") and the raw chunk text.
/// </summary>
public sealed class RetrievedKnowledgeSnippet
{
    public string NodeName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Builds and searches the per-node knowledge-chunk RAG index for the V2
/// "Graph/Blueprint" pipeline (2026-07-20) — see
/// <see cref="Models.Capabilities.Graph.CapabilityGraphNodeKnowledgeChunk"/>'s
/// doc comment for the full rationale and
/// /memories/repo/tutor-document-wide-context-gap.md for the design
/// discussion.
///
/// <see cref="IndexNodeAsync"/> is called ONCE per node, right after
/// <see cref="CapabilityGraphPersistenceService"/> persists a freshly
/// generated graph (see <see cref="PdfCapabilityGraphPipelineService"/>) —
/// chunks the node's own AcademicDefinition/Interpretation/Examples/
/// Applications fields and embeds each chunk. <see cref="IndexKnowledgeExpansionAsync"/>
/// is called opportunistically whenever <see cref="KnowledgeExpansionService"/>
/// generates a new "Profundizar" result for a node, adding its content as
/// extra chunks without touching the node's base chunks.
///
/// <see cref="SearchAsync"/> is called at Tutor-turn time
/// (<see cref="TutorService"/>) — embeds the student's own message and
/// returns the top-K most semantically similar chunks from OTHER nodes in
/// the SAME CapabilityGraph (the current node's own content already flows
/// in separately via StepContent, so it's excluded here to avoid
/// duplication).
///
/// Stateless / Singleton-safe: takes its HumanOsDbContext per call, same
/// pattern as every other Studio/Runtime service in this codebase.
/// </summary>
public sealed class NodeKnowledgeIndexService
{
    private readonly CapabilityEmbeddingService _embeddingService;

    public NodeKnowledgeIndexService(CapabilityEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    public bool IsConfigured => _embeddingService.IsConfigured;

    /// <summary>
    /// (Re)indexes a node's own base content (AcademicDefinition,
    /// Interpretation, each Example, each Application) into embedded
    /// chunks, PLUS (optionally) the raw CuradorAgent chunk(s) this node is
    /// traceable to (see <paramref name="sourceChunks"/>). Idempotent:
    /// removes any previously-indexed chunks for THESE source fields (not
    /// KnowledgeExpansion, which is managed separately) before re-adding,
    /// so it's safe to call again if a node is re-enriched later.
    /// Best-effort by design — callers should swallow exceptions from this
    /// (see <see cref="PdfCapabilityGraphPipelineService"/>) so a
    /// RAG-indexing failure never blocks the rest of capability creation.
    /// </summary>
    /// <param name="sourceChunks">
    /// The full curated corpus's chunks (2026-07-20 — real-run bug fix: a
    /// legal/regulatory PDF's specific article numbers and named entities
    /// can survive verbatim in CuradorAgent's raw chunk text yet still get
    /// paraphrased away by the time GraphArchitectAgent synthesizes a
    /// node's AcademicDefinition/Examples. Indexing the RAW chunk text too
    /// — not just the node's synthesized fields — gives the Tutor's RAG
    /// search a verbatim fallback for "what does Article X say?"-style
    /// factual questions even if the synthesized fields lost some detail.
    /// Only chunks whose Tag is listed in <c>node.ReferencesJson</c> are
    /// indexed for this node, tagged SourceField="SourceMaterial". Pass
    /// null/empty to skip this (e.g. call sites that don't have the corpus
    /// in scope) — existing behavior is unchanged in that case.
    /// </param>
    public async Task IndexNodeAsync(
        HumanOsDbContext dbContext,
        CapabilityGraphNode node,
        IReadOnlyList<CuratedChunk>? sourceChunks = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return;
        }

        var staleChunks = await dbContext.CapabilityGraphNodeKnowledgeChunks
            .Where(c => c.CapabilityGraphNodeId == node.CapabilityGraphNodeId && c.SourceField != "KnowledgeExpansion")
            .ToListAsync(cancellationToken);
        if (staleChunks.Count > 0)
        {
            dbContext.CapabilityGraphNodeKnowledgeChunks.RemoveRange(staleChunks);
        }

        var pieces = new List<(string SourceField, int SortOrder, string Content)>();

        if (!string.IsNullOrWhiteSpace(node.AcademicDefinition))
        {
            var chunks = TextChunker.Chunk(node.AcademicDefinition);
            for (var i = 0; i < chunks.Count; i++)
            {
                pieces.Add(("AcademicDefinition", i, chunks[i]));
            }
        }

        if (!string.IsNullOrWhiteSpace(node.Interpretation))
        {
            var chunks = TextChunker.Chunk(node.Interpretation);
            for (var i = 0; i < chunks.Count; i++)
            {
                pieces.Add(("Interpretation", i, chunks[i]));
            }
        }

        AddJsonListPieces(pieces, node.ExamplesJson, "Example");
        AddJsonListPieces(pieces, node.ApplicationsJson, "Application");

        if (sourceChunks is { Count: > 0 } && !string.IsNullOrWhiteSpace(node.ReferencesJson))
        {
            var referencedTags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(node.ReferencesJson)
                ?? [];
            var matchingChunks = sourceChunks
                .Where(c => !string.IsNullOrWhiteSpace(c.Content)
                    && referencedTags.Any(refTag => ChunkTagMatchesReference(c.Tag, refTag)))
                .ToList();
            for (var i = 0; i < matchingChunks.Count; i++)
            {
                var chunks = TextChunker.Chunk(matchingChunks[i].Content);
                for (var j = 0; j < chunks.Count; j++)
                {
                    pieces.Add(("SourceMaterial", i * 1000 + j, chunks[j]));
                }
            }
        }

        var newChunks = new List<CapabilityGraphNodeKnowledgeChunk>();
        foreach (var piece in pieces)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(piece.Content, cancellationToken);
            newChunks.Add(new CapabilityGraphNodeKnowledgeChunk
            {
                CapabilityGraphNodeKnowledgeChunkId = Guid.NewGuid(),
                CapabilityGraphNodeId = node.CapabilityGraphNodeId,
                CapabilityGraphId = node.CapabilityGraphId,
                SourceField = piece.SourceField,
                SortOrder = piece.SortOrder,
                Content = piece.Content,
                Embedding = new SqlVector<float>(embedding)
            });
        }

        if (newChunks.Count > 0)
        {
            dbContext.CapabilityGraphNodeKnowledgeChunks.AddRange(newChunks);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Adds the given "Profundizar" (Knowledge Expansion) HTML content as
    /// extra chunks for a node, on top of whatever <see cref="IndexNodeAsync"/>
    /// already indexed — additive only, never removes existing chunks.
    /// Best-effort: callers should swallow exceptions from this.
    /// </summary>
    public async Task IndexKnowledgeExpansionAsync(
        HumanOsDbContext dbContext,
        Guid capabilityGraphNodeId,
        Guid capabilityGraphId,
        string expansionContent,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(expansionContent))
        {
            return;
        }

        var chunks = TextChunker.Chunk(expansionContent);
        var newChunks = new List<CapabilityGraphNodeKnowledgeChunk>();
        for (var i = 0; i < chunks.Count; i++)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(chunks[i], cancellationToken);
            newChunks.Add(new CapabilityGraphNodeKnowledgeChunk
            {
                CapabilityGraphNodeKnowledgeChunkId = Guid.NewGuid(),
                CapabilityGraphNodeId = capabilityGraphNodeId,
                CapabilityGraphId = capabilityGraphId,
                SourceField = "KnowledgeExpansion",
                SortOrder = i,
                Content = chunks[i],
                Embedding = new SqlVector<float>(embedding)
            });
        }

        if (newChunks.Count > 0)
        {
            dbContext.CapabilityGraphNodeKnowledgeChunks.AddRange(newChunks);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Embeds <paramref name="queryText"/> (the student's own message) and
    /// returns the top <paramref name="topK"/> most similar chunks from
    /// OTHER nodes (excludes <paramref name="excludeNodeId"/>, the current
    /// node — its content already flows in separately as StepContent) in
    /// the SAME <paramref name="capabilityGraphId"/>. Returns an empty list
    /// (never throws) if the index isn't configured or has no rows yet for
    /// this graph — RAG is a best-effort supplement, not a hard
    /// requirement for a Tutor turn to proceed.
    /// </summary>
    public async Task<List<RetrievedKnowledgeSnippet>> SearchAsync(
        HumanOsDbContext dbContext,
        Guid capabilityGraphId,
        Guid excludeNodeId,
        string queryText,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(queryText))
        {
            return [];
        }

        try
        {
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(queryText, cancellationToken);
            var queryVector = new SqlVector<float>(queryEmbedding);

            return await dbContext.CapabilityGraphNodeKnowledgeChunks
                .AsNoTracking()
                .Where(c => c.CapabilityGraphId == capabilityGraphId && c.CapabilityGraphNodeId != excludeNodeId)
                .OrderBy(c => EF.Functions.VectorDistance("cosine", c.Embedding, queryVector))
                .Take(topK)
                .Select(c => new RetrievedKnowledgeSnippet
                {
                    NodeName = c.CapabilityGraphNode!.Name,
                    Content = c.Content
                })
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort — a search failure (e.g. transient embedding API
            // error) must never block the Tutor turn itself.
            return [];
        }
    }

    /// <summary>
    /// True if <paramref name="chunkTag"/> (a <see cref="CuratedChunk"/>'s Tag,
    /// possibly prefixed by <see cref="PdfCapabilityGraphPipelineService"/> as
    /// "Cap.N Label — {originalTag}" for multi-chapter documents) refers to
    /// the same chunk as <paramref name="referencedTag"/> (one of the SHORT
    /// tags GraphArchitectAgent echoes back in a node's References — it only
    /// ever sees/repeats the original short tag, never the chapter prefix).
    /// An exact-only comparison silently matches ZERO chunks for any
    /// multi-chapter document (2026-07-20 bug found via TAXIVA.pdf retest),
    /// so this also accepts the chunk tag ENDING WITH "— {referencedTag}",
    /// falling back to a substring check for any other separator variant.
    /// </summary>
    internal static bool ChunkTagMatchesReference(string chunkTag, string referencedTag)
    {
        if (string.IsNullOrWhiteSpace(chunkTag) || string.IsNullOrWhiteSpace(referencedTag))
        {
            return false;
        }

        if (string.Equals(chunkTag, referencedTag, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmedTag = referencedTag.Trim();
        if (chunkTag.EndsWith("— " + trimmedTag, StringComparison.OrdinalIgnoreCase)
            || chunkTag.EndsWith("—" + trimmedTag, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return chunkTag.Contains(trimmedTag, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddJsonListPieces(
        List<(string SourceField, int SortOrder, string Content)> pieces,
        string? json,
        string sourceField)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        List<string>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (JsonException)
        {
            return;
        }

        if (items is null)
        {
            return;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(items[i]))
            {
                pieces.Add((sourceField, i, items[i]));
            }
        }
    }
}
