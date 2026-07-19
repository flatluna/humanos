using HumanOS.Agents.Studio;
using Microsoft.Agents.AI.Workflows;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Workflow executor wrapping <see cref="CuradorAgent"/> — the first step
/// of the Human OS Studio capability-creation pipeline (see
/// /memories/repo/humanstudio-multiagent-vision.md). Entry point of the
/// workflow graph.
///
/// CHAPTER LOOP (2026-07-16): a large PDF (e.g. a whole textbook) can
/// easily exceed what a single Curador prompt can meaningfully digest.
/// Before curating, any Pdf raw material large enough to plausibly
/// contain multiple chapters is run through <see cref="TocExtractionAgent"/>
/// to detect its own chapter boundaries, then split into one raw-material
/// batch PER CHAPTER (via ordinal search for each chapter's StartMarker in
/// the source text — pure code, no LLM call). CuradorAgent.CurateAsync is
/// then called ONCE PER BATCH (not once for the whole document), and the
/// resulting CuratedCorpus objects are merged into one before being handed
/// to Arquitecto. Arquitecto itself is UNCHANGED and still runs exactly
/// once, over the merged corpus — it still independently designs its own
/// Level x Metric pedagogical structure; the chapter loop only improves
/// how completely/accurately the source material reaches it. Small PDFs
/// and non-Pdf materials (UserNote/txt notes, the objective) are curated
/// in a single batch exactly as before — this is a superset of, not a
/// replacement for, the previous single-call behavior.
/// </summary>
internal sealed class CuradorExecutor : Executor<RawMaterialBatch, CuratorOutput>
{
    /// <summary>Below this size, a Pdf item is curated as a single batch —
    /// not worth the extra TocExtraction call for a short document.</summary>
    private const int MinCharsForChapterSplit = 8_000;

    private readonly CuradorAgent _agent;
    private readonly TocExtractionAgent _tocExtraction;

    public CuradorExecutor(CuradorAgent agent, TocExtractionAgent tocExtraction) : base("Curador")
    {
        _agent = agent;
        _tocExtraction = tocExtraction;
    }

    public override async ValueTask<CuratorOutput> HandleAsync(
        RawMaterialBatch batch,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var materialBatches = await BuildMaterialBatchesAsync(batch.Materials, cancellationToken);

        var chunks = new List<CuratedChunk>();
        var summaryParts = new List<string>();
        var inputTokens = 0;
        var outputTokens = 0;
        var cachedInputTokens = 0;

        foreach (var materials in materialBatches)
        {
            var result = await _agent.CurateAsync(materials, cancellationToken);

            chunks.AddRange(result.Corpus.Chunks);
            if (!string.IsNullOrWhiteSpace(result.Corpus.Summary))
            {
                summaryParts.Add(result.Corpus.Summary);
            }

            inputTokens += result.TokenUsage.InputTokens;
            outputTokens += result.TokenUsage.OutputTokens;
            cachedInputTokens += result.TokenUsage.CachedInputTokens;
        }

        var corpus = new CuratedCorpus
        {
            Summary = string.Join("\n\n", summaryParts),
            Chunks = chunks
        };

        return new CuratorOutput
        {
            CapabilityDomainId = batch.CapabilityDomainId,
            CapabilityGoal = batch.CapabilityGoal,
            Corpus = corpus,
            TokenUsage = new AgentTokenUsage
            {
                AgentName = "Curador",
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CachedInputTokens = cachedInputTokens
            }
        };
    }

    /// <summary>Groups the run's raw materials into one or more batches,
    /// each of which becomes exactly one <see cref="CuradorAgent.CurateAsync"/>
    /// call. Non-Pdf materials (the objective, .txt/.md notes) always form
    /// their own batch so they aren't diluted across per-chapter Pdf
    /// batches. Each Pdf item becomes either a single batch (small PDFs, or
    /// when TocExtraction isn't configured/fails) or one batch per detected
    /// chapter (large PDFs).</summary>
    private async Task<List<List<RawMaterialItem>>> BuildMaterialBatchesAsync(
        IReadOnlyList<RawMaterialItem> materials, CancellationToken cancellationToken)
    {
        var nonPdf = materials.Where(m => m.Type != RawMaterialType.Pdf).ToList();
        var pdfs = materials.Where(m => m.Type == RawMaterialType.Pdf).ToList();

        var batches = new List<List<RawMaterialItem>>();

        if (nonPdf.Count > 0)
        {
            batches.Add(nonPdf);
        }

        foreach (var pdf in pdfs)
        {
            if (pdf.Content.Length < MinCharsForChapterSplit || !_tocExtraction.IsConfigured)
            {
                batches.Add([pdf]);
                continue;
            }

            List<RawMaterialItem> chapterMaterials;
            try
            {
                chapterMaterials = await SplitPdfIntoChapterMaterialsAsync(pdf, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // TocExtraction is a best-effort optimization; on failure,
                // fall back to curating the whole PDF in a single batch
                // rather than failing the entire capability-creation run.
                batches.Add([pdf]);
                continue;
            }

            batches.AddRange(chapterMaterials.Select(m => new List<RawMaterialItem> { m }));
        }

        // Defensive: never return zero batches (StartCapabilityCreationFunction
        // already requires at least one raw material, but guard here too).
        if (batches.Count == 0)
        {
            batches.Add([.. materials]);
        }

        return batches;
    }

    /// <summary>Runs <see cref="TocExtractionAgent"/> on one Pdf item's
    /// text and splits it into one <see cref="RawMaterialItem"/> per
    /// detected chapter, using ordinal string search for each chapter's
    /// StartMarker. Falls back to the whole PDF as a single item if fewer
    /// than two chapter boundaries can actually be located in the text
    /// (e.g. the model hallucinated a marker that isn't a verbatim match).</summary>
    private async Task<List<RawMaterialItem>> SplitPdfIntoChapterMaterialsAsync(
        RawMaterialItem pdf, CancellationToken cancellationToken)
    {
        var toc = await _tocExtraction.ExtractAsync(pdf.Content, cancellationToken);

        if (toc.Chapters.Count <= 1)
        {
            return [pdf];
        }

        var boundaries = new List<(string Title, int Index)>();
        foreach (var chapter in toc.Chapters)
        {
            if (string.IsNullOrWhiteSpace(chapter.StartMarker))
            {
                continue;
            }

            var index = pdf.Content.IndexOf(chapter.StartMarker, StringComparison.Ordinal);
            if (index >= 0)
            {
                boundaries.Add((chapter.Title, index));
            }
        }

        if (boundaries.Count <= 1)
        {
            return [pdf];
        }

        boundaries = [.. boundaries.OrderBy(b => b.Index)];

        var result = new List<RawMaterialItem>();
        for (var i = 0; i < boundaries.Count; i++)
        {
            var start = boundaries[i].Index;
            var end = i + 1 < boundaries.Count ? boundaries[i + 1].Index : pdf.Content.Length;
            var chapterText = pdf.Content[start..end];

            if (string.IsNullOrWhiteSpace(chapterText))
            {
                continue;
            }

            result.Add(new RawMaterialItem
            {
                Type = RawMaterialType.Pdf,
                Label = $"{pdf.Label} — {boundaries[i].Title}",
                Content = chapterText
            });
        }

        return result.Count > 0 ? result : [pdf];
    }
}
