using System.Text.RegularExpressions;
using HumanOS.Services.DocumentClassification;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Type-aware "Tier 0.5" chapter/node boundary detection (2026-08-17,
/// Paso 3b) — for DocumentTypes whose atomic unit is a reliable, regularly
/// numbered marker (ARTICULO for Law, CLAUSULA for Contract, PASO for
/// Procedure, IMRaD headings for ResearchPaper), this finds every marker
/// occurrence via ordinal regex search and returns ONE node per marker,
/// grouped under a single synthetic chapter — no LLM call, purely
/// deterministic, same StartMarker/ordinal-search contract as
/// <see cref="SyllabusOutlineParser"/>/<see cref="DocumentChapterSplitter"/>.
///
/// Deliberately does NOT cover Policy/AcademicWithIndex/AcademicNarrative/
/// Unknown: Policy has no reliable numeric marker (falls back to the
/// existing generic tiers unchanged); AcademicWithIndex is already well
/// served by <see cref="SyllabusOutlineParser"/>; AcademicNarrative has no
/// marker by definition (needs the existing LLM-inferred Tier 2).
/// </summary>
public static partial class DocumentTypeOutlineParser
{
    public sealed class TypeOutlineNode
    {
        public string Title { get; set; } = string.Empty;
        public int StartOffset { get; set; }
    }

    /// <summary>One real chapter grouping several nodes — e.g. one
    /// "CAPITULO I" grouping every Artículo that falls under it.</summary>
    public sealed class TypeOutlineChapter
    {
        public string Title { get; set; } = string.Empty;
        public int StartOffset { get; set; }
        public List<TypeOutlineNode> Nodes { get; set; } = [];
    }

    public sealed class TypeOutlineResult
    {
        public bool IsConfident { get; set; }
        public List<TypeOutlineChapter> Chapters { get; set; } = [];
    }

    /// <summary>Minimum marker occurrences before this is trusted as real
    /// structure rather than a coincidental single match.</summary>
    private const int MinMarkersForConfident = 2;

    private static readonly Dictionary<DocumentType, (List<Regex> NodePatterns, string FallbackChapterTitle)> MarkerPatterns = new()
    {
        // Spanish/LatAm convention tried first (".-" header marker is more
        // specific/reliable than the English fallback below) — see
        // LawArticuloRegex's own doc comment. EnglishArticleSectionRegex
        // covers US Code/UK/EU-style "Section 501."/"Article 5."/"§ 501."
        // laws, tried second since English headers are a weaker signal
        // (2026-08-17: no jurisdiction-neutral marker exists for ALL laws
        // worldwide, so this list can always grow with more conventions).
        [DocumentType.Law] = ([LawArticuloRegex(), EnglishArticleSectionRegex()], "Artículos"),
        [DocumentType.Contract] = ([ContractClausulaRegex()], "Cláusulas"),
        [DocumentType.Procedure] = ([ProcedurePasoRegex()], "Pasos"),
        [DocumentType.ResearchPaper] = ([ResearchPaperHeadingRegex()], "Secciones")
    };

    /// <summary>Real chapter-level markers, ONLY for the DocumentTypes that
    /// actually use them (Law's own "CAPITULO N" — reused from
    /// LawClassifier's own pattern). When present, nodes are grouped under
    /// their enclosing chapter instead of one flat synthetic chapter.</summary>
    private static readonly Dictionary<DocumentType, Regex> ChapterPatterns = new()
    {
        [DocumentType.Law] = LawCapituloRegex()
    };

    /// <summary>Returns a confident result only when the DocumentType has a
    /// known marker pattern AND that pattern occurs at least
    /// <see cref="MinMarkersForConfident"/> times in documentText. When the
    /// type also has a real chapter-level marker (see
    /// <see cref="ChapterPatterns"/>) and it's actually present, nodes are
    /// grouped under their real enclosing chapter (e.g. "CAPITULO I") —
    /// otherwise every node falls under one flat synthetic chapter (e.g.
    /// "Artículos"), same as before this grouping existed.</summary>
    public static TypeOutlineResult Parse(string documentText, DocumentType documentType)
    {
        if (!MarkerPatterns.TryGetValue(documentType, out var config) || string.IsNullOrWhiteSpace(documentText))
        {
            return new TypeOutlineResult { IsConfident = false };
        }

        var nodeMatches = config.NodePatterns
            .Select(pattern => pattern.Matches(documentText))
            .FirstOrDefault(matches => matches.Count >= MinMarkersForConfident);
        if (nodeMatches is null)
        {
            return new TypeOutlineResult { IsConfident = false };
        }

        var nodeOffsets = nodeMatches.Select(match => match.Index).OrderBy(offset => offset).ToList();

        List<int> chapterOffsets = [];
        if (ChapterPatterns.TryGetValue(documentType, out var chapterPattern))
        {
            chapterOffsets = chapterPattern.Matches(documentText).Select(match => match.Index).OrderBy(offset => offset).ToList();
        }

        // Every detected boundary (chapter AND node), sorted — used to bound
        // each title so it stops right before whichever marker comes next,
        // instead of a fixed length/newline heuristic that can't tell where
        // a PDF-extracted, newline-collapsed heading actually ends (2026-08-17
        // fix: a CAPITULO title was swallowing the following Artículo's
        // opening words).
        var allBoundaryOffsets = nodeOffsets.Concat(chapterOffsets).OrderBy(offset => offset).ToList();

        var nodes = nodeOffsets
            .Select(offset => new TypeOutlineNode { Title = BuildBoundedTitle(documentText, offset, allBoundaryOffsets), StartOffset = offset })
            .ToList();

        var chapterBoundaries = chapterOffsets
            .Select(offset => (Title: BuildBoundedTitle(documentText, offset, allBoundaryOffsets), StartOffset: offset))
            .ToList();

        if (chapterBoundaries.Count == 0)
        {
            // No real chapter markers for this type/document — one flat
            // synthetic chapter holding every node, same as before.
            return new TypeOutlineResult
            {
                IsConfident = true,
                Chapters = [new TypeOutlineChapter { Title = config.FallbackChapterTitle, StartOffset = nodes[0].StartOffset, Nodes = nodes }]
            };
        }

        // Group each node under the LAST chapter boundary that starts at or
        // before it — nodes before the very first chapter boundary (rare,
        // e.g. a preamble article) fall under a leading synthetic chapter.
        var chapters = new List<TypeOutlineChapter>();
        foreach (var boundary in chapterBoundaries)
        {
            chapters.Add(new TypeOutlineChapter { Title = boundary.Title, StartOffset = boundary.StartOffset });
        }

        var leading = new TypeOutlineChapter { Title = config.FallbackChapterTitle, StartOffset = 0 };
        foreach (var node in nodes)
        {
            var owningChapter = chapters.LastOrDefault(chapter => chapter.StartOffset <= node.StartOffset);
            (owningChapter ?? leading).Nodes.Add(node);
        }

        var result = new List<TypeOutlineChapter>();
        if (leading.Nodes.Count > 0)
        {
            result.Add(leading);
        }
        result.AddRange(chapters.Where(chapter => chapter.Nodes.Count > 0));

        return new TypeOutlineResult { IsConfident = result.Count > 0, Chapters = result };
    }

    /// <summary>Short human-readable title for a boundary at
    /// <paramref name="offset"/>: everything from that offset up to
    /// whichever comes first — the NEXT detected boundary (chapter or
    /// node), a line break, or <see cref="MaxTitleLength"/> characters —
    /// so a chapter heading never bleeds into the next article/section's
    /// own opening words when the source text has no real line breaks
    /// left after PDF extraction.</summary>
    private const int MaxTitleLength = 80;

    private static string BuildBoundedTitle(string documentText, int offset, List<int> allBoundaryOffsets)
    {
        if (offset < 0 || offset >= documentText.Length)
        {
            return string.Empty;
        }

        var nextBoundaryOffset = allBoundaryOffsets.FirstOrDefault(candidate => candidate > offset, documentText.Length);
        var hardLimit = Math.Min(nextBoundaryOffset, offset + MaxTitleLength);
        var length = Math.Min(hardLimit, documentText.Length) - offset;
        if (length <= 0)
        {
            return string.Empty;
        }

        var excerpt = documentText.Substring(offset, length);
        var newlineIndex = excerpt.IndexOfAny(['\r', '\n']);
        var title = (newlineIndex >= 0 ? excerpt[..newlineIndex] : excerpt).Trim();
        return string.IsNullOrWhiteSpace(title) ? excerpt.Trim() : title;
    }

    /// <summary>Matches a real Artículo HEADER, never an inline reference
    /// to another article (2026-08-17 fix — PDF text extraction wraps
    /// lines arbitrarily, so anchoring to line-start is unreliable: an
    /// inline reference like "los artículos 1o.-A, 3o. ... 18-J" can land
    /// at the start of a wrapped line purely by coincidence). The real,
    /// content-based signal in Mexican legal drafting is that a header
    /// ALWAYS ends with a literal ".-" immediately after the article
    /// number/suffix ("Artículo 1o.-", "Artículo 1o.-A.-", "Artículo
    /// 1o.-A BIS.-"), while an inline reference ("artículo 18-B de la",
    /// "el artículo 18-J de este ordenamiento") never has that ".-"
    /// marker right after the number — and the plural "artículos" (always
    /// a reference, never a header) is excluded entirely since \s+ after
    /// the literal word "ARTICULO" cannot match "S".</summary>
    [GeneratedRegex(@"\bART[IÍ]CULO\s+\d+\s*[o°]?\.?\s*(-\s*[A-ZÑ]+(\s*BIS)?)?\s*\.-", RegexOptions.IgnoreCase)]
    private static partial Regex LawArticuloRegex();

    /// <summary>Anglophone law header convention (US Code/UK/EU-style):
    /// "Section 501.", "Sec. 501.", "§ 501.", or "Article 5." — requires the
    /// number to be followed by a period and then a capitalized word
    /// (mimicking a title beginning), so an inline reference like "under
    /// Section 501 of this title" (lowercase continuation, no period right
    /// after the number) does not match.</summary>
    [GeneratedRegex(@"\b(SECTION|SEC\.|ARTICLE|§)\s*\d+[A-Za-z]?\.\s+[A-Z]", RegexOptions.IgnoreCase)]
    private static partial Regex EnglishArticleSectionRegex();

    /// <summary>Real chapter-level markers for Law: "CAPITULO N", "TITULO N",
    /// "SECCION N" (Roman or Arabic numerals), or a plain numbered heading
    /// like "1.1 Nombre" at the start of a line — covers laws that group
    /// their Artículos under any of these instead of only "CAPITULO".</summary>
    [GeneratedRegex(@"(\b(CAP[IÍ]TULO|T[IÍ]TULO|SECCI[OÓ]N)\s+[IVXLCDM\d]+)|(^\s*\d+(\.\d+)+\s+\S)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex LawCapituloRegex();

    [GeneratedRegex(@"\bCL[AÁ]USULA\s+[IVXLCDM\d]+", RegexOptions.IgnoreCase)]
    private static partial Regex ContractClausulaRegex();

    [GeneratedRegex(@"\b(PASO|STEP|ACTIVIDAD)\s+\d+", RegexOptions.IgnoreCase)]
    private static partial Regex ProcedurePasoRegex();

    [GeneratedRegex(@"^\s*(ABSTRACT|RESUMEN|INTRODUCTION|INTRODUCCI[OÓ]N|METHODOLOGY|METHODS|METODOLOG[IÍ]A|RESULTS|RESULTADOS|DISCUSSION|DISCUSI[OÓ]N|CONCLUSIONS?|CONCLUSIONES|REFERENCES|REFERENCIAS)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ResearchPaperHeadingRegex();
}
