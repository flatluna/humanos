namespace HumanOS.Agents.Studio;

/// <summary>
/// Shared chapter-splitting logic: given a document's raw text, uses
/// <see cref="TocExtractionAgent"/> to detect chapter boundaries and splits
/// the text into one <see cref="RawMaterialItem"/> per chapter via ordinal
/// string search (pure code, no LLM call for the actual slicing). Extracted
/// out of <see cref="HumanOS.Agentic.Studio.CuradorExecutor"/> (V1's
/// capability-creation workflow, 2026-07-16) so the V2 PDF→CapabilityGraph
/// pipeline can reuse the EXACT same, already-proven algorithm instead of a
/// second implementation — see
/// /memories/repo/humanstudio-multiagent-vision.md.
///
/// V1's CuradorExecutor still owns its own size threshold
/// (MinCharsForChapterSplit) and best-effort-fallback-on-error wrapping —
/// this class only owns the "detect chapters, then slice text" mechanics
/// shared by both callers.
/// </summary>
public static class DocumentChapterSplitter
{
    /// <summary>
    /// Runs <see cref="TocExtractionAgent"/> on one document's text and
    /// splits it into one <see cref="RawMaterialItem"/> per detected
    /// chapter, using ordinal string search for each chapter's
    /// StartMarker. Falls back to the whole document as a single item if
    /// fewer than two chapter boundaries can actually be located in the
    /// text (e.g. the model hallucinated a marker that isn't a verbatim
    /// match, or the source document genuinely has no internal chapter
    /// structure — a short note, a single-topic article, a legal document
    /// whose sections aren't pedagogical units, etc).
    /// </summary>
    public static async Task<List<RawMaterialItem>> SplitIntoChapterMaterialsAsync(
        RawMaterialItem source,
        TocExtractionAgent tocExtraction,
        CancellationToken cancellationToken = default)
    {
        var toc = await tocExtraction.ExtractAsync(source.Content, cancellationToken);

        if (toc.Chapters.Count <= 1)
        {
            return [source];
        }

        var boundaries = new List<(string Title, int Index)>();
        foreach (var chapter in toc.Chapters)
        {
            if (string.IsNullOrWhiteSpace(chapter.StartMarker))
            {
                continue;
            }

            var index = source.Content.IndexOf(chapter.StartMarker, StringComparison.Ordinal);
            if (index >= 0)
            {
                boundaries.Add((chapter.Title, index));
            }
        }

        if (boundaries.Count <= 1)
        {
            return [source];
        }

        boundaries = [.. boundaries.OrderBy(b => b.Index)];

        var result = new List<RawMaterialItem>();
        for (var i = 0; i < boundaries.Count; i++)
        {
            var start = boundaries[i].Index;
            var end = i + 1 < boundaries.Count ? boundaries[i + 1].Index : source.Content.Length;
            var chapterText = source.Content[start..end];

            if (string.IsNullOrWhiteSpace(chapterText))
            {
                continue;
            }

            result.Add(new RawMaterialItem
            {
                Type = source.Type,
                Label = $"{source.Label} — {boundaries[i].Title}",
                Content = chapterText
            });
        }

        return result.Count > 0 ? result : [source];
    }
}
