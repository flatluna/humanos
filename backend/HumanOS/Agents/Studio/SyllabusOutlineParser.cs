using System.Text.RegularExpressions;

namespace HumanOS.Agents.Studio;

/// <summary>One detected numbered subsection (e.g. "1.1") within a <see cref="SyllabusModule"/>.</summary>
public sealed class SyllabusSubsection
{
    /// <summary>The subsection's own number exactly as it appears (e.g. "1.1", "1.0").</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>The subsection's heading text, numbering stripped (e.g. "¿Qué es el marketing digital?").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Character offset where this subsection's heading line starts in the original document text.</summary>
    public int StartOffset { get; set; }
}

/// <summary>One detected top-level module/unit/chapter of a syllabus.</summary>
public sealed class SyllabusModule
{
    /// <summary>The module's own number exactly as it appears (e.g. 1, 2, 3...).</summary>
    public int Number { get; set; }

    /// <summary>The heading keyword exactly as it appeared in the source
    /// document (e.g. "Módulo", "Unidad", "Capítulo", "Unit", "Chapter") —
    /// preserved so downstream chapter labels/titles reflect the document's
    /// own vocabulary instead of always saying "Módulo".</summary>
    public string Keyword { get; set; } = "Módulo";

    /// <summary>The module's heading text, numbering stripped (e.g. "Introducción al Marketing Digital").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Character offset where this module's heading line starts in the original document text.</summary>
    public int StartOffset { get; set; }

    public List<SyllabusSubsection> Subsections { get; set; } = [];
}

/// <summary>Result of attempting deterministic (regex-based, no LLM) syllabus
/// structure detection over a document's raw text.</summary>
public sealed class SyllabusOutlineParseResult
{
    /// <summary>True only when the detected structure passed every quality
    /// gate in <see cref="SyllabusOutlineParser.Parse"/> and can be trusted
    /// as the document's real, authoritative module/subsection index.</summary>
    public bool IsConfidentStructure { get; set; }

    /// <summary>Detected modules, in document order. Empty when
    /// <see cref="IsConfidentStructure"/> is false.</summary>
    public List<SyllabusModule> Modules { get; set; } = [];

    /// <summary>Human-readable (Spanish) explanation of why the structure
    /// was rejected — set only when <see cref="IsConfidentStructure"/> is
    /// false, intended to be surfaced directly to the course creator so
    /// they can fix their document's index rather than receive a silent,
    /// lower-quality fallback.</summary>
    public string? RejectionReason { get; set; }
}

/// <summary>
/// Thrown when the caller opted into "Estructurado" (<c>strictSyllabusMode
/// = true</c>) course creation but NEITHER Tier 0 (<see
/// cref="SyllabusOutlineParser"/>) NOR Tier 1 (<see cref="TocExtractionAgent"/>)
/// could confidently detect the document's numbered module/subsection
/// structure — per the 2026-08-03 design decision, this case must be
/// surfaced as a clear, actionable failure (never silently downgraded to
/// a lower-quality "Libre" run). The message is written for the end user
/// (course creator), in Spanish, and is what ultimately reaches the
/// frontend via <c>PdfCapabilityGraphRunStatus.Failed(runId, ex.Message)</c>.
/// </summary>
public sealed class SyllabusStructureNotFoundException(string detail) : Exception(
    "No se pudo detectar una estructura de índice confiable en el documento (módulos/unidades numerados, " +
    "ej. 'Módulo 1: ...', 'Unidad 2: ...', con subtemas numerados como '1.1', '1.2'). " +
    "El modo Estructurado requiere un índice claro y consistente. Revisa y corrige el índice del documento, " +
    $"o vuelve a intentarlo en modo Libre. Detalle: {detail}")
{
    /// <summary>The specific Tier 0/Tier 1 rejection reason, for logging/debugging.</summary>
    public string Detail { get; } = detail;
}

/// <summary>
/// Deterministic (no LLM call), regex-based detector for a formally
/// numbered course syllabus already present in a document's own raw text
/// (e.g. "### Módulo 1: Título" + "- 1.1. Subtítulo" bullets). Zero cost,
/// instant, and exact when it matches — no risk of an LLM paraphrasing or
/// silently dropping the user's own numbering/wording. Recognizes several
/// common Spanish/English module-keyword conventions (Módulo/Unidad/
/// Capítulo/Tema/Sección/Unit/Chapter/Section/Part) and numbered
/// subsection bullets ("1.1", "1.2", ...).
///
/// This is "Tier 0" of a 3-tier structure-detection strategy for course
/// creation (2026-08-03 design decision — see /memories/repo):
///   Tier 0 (this class): deterministic regex — preferred whenever it
///     confidently matches; used on the RAW input text (an uploaded
///     PDF's extracted text, OR the user's own pasted description/idea
///     text) BEFORE any LLM expansion/detection step runs, so a fully
///     structured syllabus the user pastes verbatim is never rewritten
///     or reinterpreted by an LLM before its structure is captured.
///   Tier 1: TocExtractionAgent (LLM) — fallback when Tier 0 fails to
///     find a confident structure but the document may still have one
///     that didn't survive as clean, regex-matchable text (e.g. a real
///     PDF whose headings lost their formatting on text extraction).
///   Tier 2: reject — if the caller opted into "Estructurado" processing
///     but NEITHER tier finds a confident structure, the run must be
///     REJECTED with a clear, actionable message asking the user to fix
///     the document's index — never silently degraded to freeform
///     ("Libre") mode. See <see cref="SyllabusOutlineParseResult.RejectionReason"/>.
/// </summary>
public static class SyllabusOutlineParser
{
    private static readonly Regex ModuleHeaderRegex = new(
        @"^[#\s]*\**\s*(?<keyword>M[oó]dulo|Unidad|Cap[ií]tulo|Unit|Chapter|Tema|Secci[oó]n|Section|Part|Parte)\s+(?<number>\d+)\s*[:\-–.]?\s*(?<title>.*?)\**\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SubsectionRegex = new(
        @"^[\-\*•\s]*\**\s*(?<major>\d+)\.(?<minor>\d+)\.?\s*[:\-–]?\s*(?<title>.+?)\**\s*$",
        RegexOptions.Compiled);

    /// <summary>Keyword-prefixed subsection convention (2026-10-08) — e.g.
    /// "Lección 1. La visita al bosque", "Lesson 2: ..." — same purpose as
    /// <see cref="SubsectionRegex"/>'s "1.1"/"1.2" numeric-dot convention,
    /// for documents (chiefly YoungLearner/Academic courses) that number
    /// lessons plainly ("Lección N") instead of with a module-qualified
    /// decimal. Tried as a FALLBACK only when <see cref="SubsectionRegex"/>
    /// doesn't match a given line (see <see cref="ParseKeywordHeadings"/>),
    /// so documents already using the numeric-dot convention are entirely
    /// unaffected — purely additive, zero behavior change for any existing
    /// document/course (enterprise or academic) that already parsed
    /// correctly before this.</summary>
    private static readonly Regex SubsectionKeywordRegex = new(
        @"^[#\s]*\**\s*(?<keyword>Lecci[oó]n|Lesson|Clase|Class)\s+(?<number>\d+)\s*[:\-–.]?\s*(?<title>.*?)\**\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Matches one dot-leader Table of Contents entry ANYWHERE in
    /// the text (not anchored to a line start/end) — e.g. "1.1 Purpose of
    /// the Policy .......... 1". Used by <see cref="ParseFlatDotLeaderToc"/>
    /// for documents whose text extraction collapsed every line break, so
    /// headings never start their own line (see that method's remarks).
    /// The negative lookbehind avoids matching in the middle of a larger
    /// number (e.g. "10.5"), and requiring 4+ dots after the title avoids
    /// matching incidental "N.M" text (version numbers, dates) that isn't
    /// followed by a real dot-leader/page-number.</summary>
    private static readonly Regex FlatTocEntryRegex = new(
        @"(?<![\d.])(?<major>\d{1,2})\.(?<minor>\d{1,2})\s+(?<title>[A-Za-zÀ-ÿ][^\r\n]*?)\s*\.{4,}\s*(?<page>\d{1,4})(?!\d)",
        RegexOptions.Compiled);

    /// <summary>Bare numeric heading with no keyword prefix at all, e.g.
    /// "1.0 Introduction" (chapter, minor "0") or "3.2 General
    /// Responsibilities" (subsection of chapter 3) — the convention many
    /// real policy/HR/legal documents use instead of "Módulo"/"Unit".
    /// Requires the title to start with a letter to avoid matching stray
    /// numeric text (dates, page numbers, money amounts) as a heading.</summary>
    private static readonly Regex BareNumericHeaderRegex = new(
        @"^(?<major>\d{1,2})\.(?<minor>\d{1,2})\s+(?<title>[A-Za-zÀ-ÿ].{1,90})$",
        RegexOptions.Compiled);

    /// <summary>Matches a Markdown link "[text](url)" so its bracket/paren
    /// decoration can be stripped down to just its text — common in PDF-to-
    /// Markdown extractions that wrap every heading in an (often empty)
    /// link, e.g. "# [1.0 Introduction]()" or even "# []()[3.0 Title]()".
    /// The bracketed text itself may span multiple physical lines (a
    /// heading word-wrapped by the extractor) — matched here since
    /// "[^\[\]]" isn't newline-excluding, and rejoined by
    /// <see cref="JoinWrappedMarkdownLinks"/> before this runs.</summary>
    private static readonly Regex MarkdownLinkRegex = new(
        @"\[([^\[\]]*)\]\(([^)]*)\)", RegexOptions.Compiled);

    /// <summary>Matches a Markdown IMAGE "![alt](url)" so it can be
    /// stripped ENTIRELY (including the leading "!", unlike
    /// <see cref="MarkdownLinkRegex"/> which keeps a plain link's text) —
    /// a PDF-to-Markdown extraction commonly glues an inline image
    /// directly against the very next heading with NO separating
    /// whitespace/newline (e.g. "![](img.png)[1.1 Purpose of the
    /// Policy]()"). Without this, <see cref="MarkdownLinkRegex"/> alone
    /// would only consume the image's own "[...]()" span and leave a
    /// stray leading "!" glued onto the heading text that follows, which
    /// then fails <see cref="BareNumericHeaderRegex"/>/<see
    /// cref="ModuleHeaderRegex"/> (both require the line to START with a
    /// digit/keyword) — silently rejecting an otherwise perfectly
    /// numbered document. Must run BEFORE <see cref="MarkdownLinkRegex"/>.
    /// </summary>
    private static readonly Regex ImageMarkdownRegex = new(
        @"!\[([^\[\]]*)\]\(([^)]*)\)", RegexOptions.Compiled);

    /// <summary>
    /// Collapses a newline embedded INSIDE an unclosed "[...]" Markdown
    /// link span into a single space, char-for-char (same total length —
    /// the URL portion is preserved verbatim, so every downstream
    /// character offset into the original document text stays valid) —
    /// so a heading word-wrapped across two physical lines by the
    /// extractor (e.g. "[1.2 Importance of Volunteers in\nthe
    /// Program]()") is treated as one logical line by the line-based
    /// scanners below, instead of silently failing to match on either
    /// fragment. Replaces "\r" and "\n" SEPARATELY (not "\r\n" as one
    /// unit) so a CRLF line ending still maps 1 char -> 1 char (two
    /// spaces) rather than collapsing 2 chars into 1 and shifting every
    /// offset after it.
    /// </summary>
    private static string JoinWrappedMarkdownLinks(string documentText) =>
        MarkdownLinkRegex.Replace(documentText, m =>
            "[" + m.Groups[1].Value.Replace('\r', ' ').Replace('\n', ' ') + "](" + m.Groups[2].Value + ")");

    /// <summary>
    /// Strips heading/decoration noise a line-based regex would otherwise
    /// choke on — leading "#"/"##" markers, an image glued directly
    /// before the heading ("![alt](url)" -> removed entirely, see
    /// <see cref="ImageMarkdownRegex"/>), Markdown link wrapping
    /// ("[text]()" -> "text"), and bold/italic markers ("**"/"__") —
    /// before it's tested against <see cref="ModuleHeaderRegex"/>,
    /// <see cref="SubsectionRegex"/>, or <see cref="BareNumericHeaderRegex"/>.
    /// Applied ONLY for regex matching; the original (raw) line is still
    /// used for offset bookkeeping so slicing the source document later
    /// remains exact — no image content anywhere in the actual document/
    /// course is ever touched by this, purely a text-matching detail.
    /// </summary>
    private static string NormalizeCandidateLine(string trimmedLine)
    {
        var withoutImages = ImageMarkdownRegex.Replace(trimmedLine, string.Empty);
        var withoutHeadingMarker = Regex.Replace(withoutImages, @"^#+\s*", "");
        var withoutLinks = MarkdownLinkRegex.Replace(withoutHeadingMarker, "$1");
        var withoutBold = withoutLinks.Replace("**", string.Empty).Replace("__", string.Empty);
        return withoutBold.Trim();
    }

    /// <summary>Collapses any run of whitespace (including the double space
    /// a CRLF line break becomes once <see cref="JoinWrappedMarkdownLinks"/>
    /// replaces its "\r" and "\n" separately, one-for-one) down to a single
    /// space — applied only to already-extracted display text (titles,
    /// keywords), never to raw text still used for offset bookkeeping.
    /// </summary>
    private static string CollapseInternalWhitespace(string text) => Regex.Replace(text.Trim(), @"\s+", " ");

    /// <summary>Minimum number of top-level modules required to consider
    /// the document confidently structured — a single detected "module"
    /// is never enough signal to trust over the document's own free-form
    /// judgment.</summary>
    private const int MinModules = 2;

    /// <summary>Minimum fraction of the document's text (measured from the
    /// FIRST detected module heading through the end of the document) that
    /// must fall inside the detected module span — guards against a
    /// couple of stray regex matches deep inside an otherwise unstructured
    /// document being mistaken for a real index.</summary>
    private const double MinLeadingTextCoverage = 0.7;

    /// <summary>
    /// Attempts to deterministically detect a formal module/subsection
    /// syllabus structure in <paramref name="documentText"/>. Returns a
    /// result with <see cref="SyllabusOutlineParseResult.IsConfidentStructure"/>
    /// = false (never a partial/best-effort structure) whenever the
    /// detected pattern doesn't clear every quality gate below. Tries the
    /// keyword-prefixed convention ("Módulo 1", "Unit 2") first, then the
    /// bare-numeric convention ("1.0 Introduction", "3.2 General
    /// Responsibilities") — both are zero-cost, no-LLM detections, so
    /// trying both is cheap and keeps genuinely numbered documents out of
    /// the LLM-based Tier 1 fallback entirely.
    /// </summary>
    public static SyllabusOutlineParseResult Parse(string documentText)
    {
        if (string.IsNullOrWhiteSpace(documentText))
        {
            return Rejected("El documento está vacío.");
        }

        var joinedText = JoinWrappedMarkdownLinks(documentText);

        var keywordResult = ParseKeywordHeadings(joinedText);
        if (keywordResult.IsConfidentStructure)
        {
            return keywordResult;
        }

        var numericResult = ParseBareNumericHeadings(joinedText);
        if (numericResult.IsConfidentStructure)
        {
            return numericResult;
        }

        // Last resort: the line-based conventions above require a heading
        // to start its own line — some real PDF-to-text extractions
        // collapse ALL line breaks, gluing a document's own Table of
        // Contents and body into one continuous block of prose.
        var flatTocResult = ParseFlatDotLeaderToc(joinedText);
        if (flatTocResult.IsConfidentStructure)
        {
            return flatTocResult;
        }

        return keywordResult;
    }

    private static SyllabusOutlineParseResult ParseKeywordHeadings(string documentText)
    {
        var lines = documentText.Replace("\r\n", "\n").Split('\n');
        var modules = new List<SyllabusModule>();
        SyllabusModule? currentModule = null;
        var offset = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var candidate = trimmed.Length > 0 ? NormalizeCandidateLine(trimmed) : trimmed;

            var moduleMatch = candidate.Length > 0 ? ModuleHeaderRegex.Match(candidate) : Match.Empty;
            if (moduleMatch.Success && int.TryParse(moduleMatch.Groups["number"].Value, out var moduleNumber))
            {
                currentModule = new SyllabusModule
                {
                    Number = moduleNumber,
                    Keyword = CollapseInternalWhitespace(moduleMatch.Groups["keyword"].Value),
                    Title = CollapseInternalWhitespace(moduleMatch.Groups["title"].Value),
                    StartOffset = offset
                };
                modules.Add(currentModule);
                offset += line.Length + 1;
                continue;
            }

            var subsectionMatch = currentModule is not null && candidate.Length > 0
                ? SubsectionRegex.Match(candidate)
                : Match.Empty;
            if (subsectionMatch.Success &&
                subsectionMatch.Groups["major"].Value == currentModule!.Number.ToString())
            {
                currentModule.Subsections.Add(new SyllabusSubsection
                {
                    Number = $"{subsectionMatch.Groups["major"].Value}.{subsectionMatch.Groups["minor"].Value}",
                    Title = CollapseInternalWhitespace(subsectionMatch.Groups["title"].Value),
                    StartOffset = offset
                });
            }
            else
            {
                // Keyword-prefixed lesson convention fallback (2026-10-08) — e.g.
                // "Lección 1. La visita al bosque" instead of "1.1 ...". Only tried
                // when the numeric-dot SubsectionRegex above didn't match, so an
                // already-working numeric-dot document's parsing is bit-for-bit
                // unchanged.
                var keywordSubsectionMatch = currentModule is not null && candidate.Length > 0
                    ? SubsectionKeywordRegex.Match(candidate)
                    : Match.Empty;
                if (keywordSubsectionMatch.Success)
                {
                    currentModule!.Subsections.Add(new SyllabusSubsection
                    {
                        Number = keywordSubsectionMatch.Groups["number"].Value,
                        Title = CollapseInternalWhitespace(keywordSubsectionMatch.Groups["title"].Value),
                        StartOffset = offset
                    });
                }
            }

            offset += line.Length + 1;
        }

        // ---- Quality gate — reject rather than return a shaky partial result ----
        if (modules.Count < MinModules)
        {
            return Rejected(
                $"Solo se detectaron {modules.Count} módulo(s) — se requieren al menos {MinModules} " +
                "encabezados numerados (ej. 'Módulo 1', 'Unidad 2') para considerar el documento estructurado.");
        }

        for (var i = 1; i < modules.Count; i++)
        {
            if (modules[i].Number <= modules[i - 1].Number)
            {
                return Rejected(
                    $"La numeración de los módulos no es consistente/creciente (Módulo {modules[i - 1].Number} " +
                    $"seguido de Módulo {modules[i].Number}).");
            }
        }

        var firstModuleOffset = modules[0].StartOffset;
        var coverage = 1.0 - (double)firstModuleOffset / documentText.Length;
        if (coverage < MinLeadingTextCoverage)
        {
            return Rejected(
                $"El primer módulo detectado aparece muy tarde en el documento (cobertura {coverage:P0}) — " +
                "es probable que sea una coincidencia incidental, no un índice real.");
        }

        var modulesWithSubsections = modules.Count(m => m.Subsections.Count > 0);
        if (modulesWithSubsections > 0 && modulesWithSubsections < modules.Count / 2.0)
        {
            return Rejected(
                "Solo algunos módulos tienen subsecciones numeradas detectadas — la estructura no es " +
                "consistente en todo el documento.");
        }

        return new SyllabusOutlineParseResult
        {
            IsConfidentStructure = true,
            Modules = modules
        };
    }

    /// <summary>
    /// Detects the "N.0 Title" (chapter) / "N.M Title" (subsection of
    /// chapter N) convention with no keyword prefix at all (e.g. a policy
    /// document's own "1.0 Introduction" / "1.1 Purpose of the Policy" /
    /// "3.2 General Responsibilities" numbering). Purely arithmetic —
    /// groups every match by its own major number, in the order chapters
    /// first appear, no LLM judgment involved. Handles documents that
    /// repeat their own numbered index twice (a "Table of Contents" page
    /// followed by the real body repeating every heading): when the same
    /// (major, minor) pair is matched more than once, keeps whichever
    /// occurrence sits LATER in the text — a TOC listing always precedes
    /// the real content it summarizes.
    /// </summary>
    private static SyllabusOutlineParseResult ParseBareNumericHeadings(string documentText)
    {
        var lines = documentText.Replace("\r\n", "\n").Split('\n');
        var offset = 0;
        var bestByNumber = new Dictionary<(int Major, int Minor), (string Title, int Offset)>();
        var numberOrder = new List<(int Major, int Minor)>();

        // Tracked separately from bestByNumber's (deduped, LATER-wins)
        // offsets: a document that opens with its own Table of Contents
        // (e.g. a Word-exported "[1.0 Introduction. 1](#_Toc...)" listing)
        // legitimately has its first REAL numbered heading appear early —
        // the coverage gate below must judge that against this earliest
        // sighting, not against the body occurrence chosen to win dedup.
        var firstSeenOffset = -1;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var candidate = trimmed.Length > 0 ? NormalizeCandidateLine(trimmed) : trimmed;
            var match = candidate.Length > 0 ? BareNumericHeaderRegex.Match(candidate) : Match.Empty;
            if (match.Success
                && int.TryParse(match.Groups["major"].Value, out var major)
                && int.TryParse(match.Groups["minor"].Value, out var minor))
            {
                if (firstSeenOffset < 0)
                {
                    firstSeenOffset = offset;
                }

                var key = (major, minor);
                var title = CollapseInternalWhitespace(match.Groups["title"].Value);
                if (!bestByNumber.TryGetValue(key, out var existing))
                {
                    numberOrder.Add(key);
                    bestByNumber[key] = (title, offset);
                }
                else if (offset > existing.Offset)
                {
                    bestByNumber[key] = (title, offset);
                }
            }

            offset += line.Length + 1;
        }

        if (numberOrder.Count == 0)
        {
            return Rejected("No se detectaron encabezados numerados del tipo '1.0', '1.1', etc.");
        }

        var modules = new List<SyllabusModule>();
        var moduleByMajor = new Dictionary<int, SyllabusModule>();

        // PASS 1 (fixed 2026-08-09 — real bug: a subsection like "1.2"
        // whose real body occurrence happened to be located AFTER a
        // later chapter's "3.0" entry — e.g. from a PDF text-extraction
        // artifact reordering a sidebar/margin paragraph — was processed
        // by the single offset-ordered pass below BEFORE its own
        // chapter's "1.0" entry ever ran, creating a throwaway
        // placeholder module for major=1 that then got silently ORPHANED
        // (left in `modules`, never merged) once the real "1.0" entry was
        // processed later and overwrote moduleByMajor[1] with a brand-new
        // module object — leaving "1.2" attached to the orphan and the
        // real "1.0 Introduction" module without it. Registering exactly
        // ONE module object per distinct major up front, before any
        // subsection can be attached, makes this impossible regardless of
        // processing order.
        foreach (var (major, _) in numberOrder)
        {
            if (moduleByMajor.ContainsKey(major))
            {
                continue;
            }

            var module = new SyllabusModule { Number = major, Keyword = string.Empty, Title = $"{major}.0" };
            modules.Add(module);
            moduleByMajor[major] = module;
        }

        // PASS 2: fill in each module's real title/offset from its own
        // "X.0" entry (if present) and attach every subsection — always to
        // the SAME module object registered above, never a duplicate.
        var majorsWithOwnOffset = new HashSet<int>();
        foreach (var (major, minor) in numberOrder.OrderBy(key => bestByNumber[key].Offset))
        {
            var (title, matchOffset) = bestByNumber[(major, minor)];
            var module = moduleByMajor[major];
            if (minor == 0)
            {
                module.Title = $"{major}.0 {title}";
                module.StartOffset = matchOffset;
                majorsWithOwnOffset.Add(major);
                continue;
            }

            if (majorsWithOwnOffset.Add(major))
            {
                // No "X.0" heading exists for this major — fall back to
                // this (its earliest, since we're iterating in offset
                // order) subsection's own offset as the chapter's start.
                module.StartOffset = matchOffset;
            }

            module.Subsections.Add(new SyllabusSubsection
            {
                Number = $"{major}.{minor}",
                Title = title,
                StartOffset = matchOffset
            });
        }

        modules = modules.OrderBy(m => m.StartOffset).ToList();

        if (modules.Count < MinModules)
        {
            return Rejected(
                $"Solo se detectaron {modules.Count} capítulo(s) numerado(s) — se requieren al menos " +
                $"{MinModules} (ej. '1.0', '2.0') para considerar el documento estructurado.");
        }

        for (var i = 1; i < modules.Count; i++)
        {
            if (modules[i].Number <= modules[i - 1].Number)
            {
                return Rejected(
                    $"La numeración de los capítulos no es consistente/creciente (Capítulo {modules[i - 1].Number} " +
                    $"seguido de Capítulo {modules[i].Number}).");
            }
        }

        var firstModuleOffset = firstSeenOffset >= 0 ? firstSeenOffset : modules[0].StartOffset;
        var coverage = 1.0 - (double)firstModuleOffset / documentText.Length;
        if (coverage < MinLeadingTextCoverage)
        {
            return Rejected(
                $"El primer capítulo numerado aparece muy tarde en el documento (cobertura {coverage:P0}) — " +
                "es probable que sea una coincidencia incidental, no un índice real.");
        }

        return new SyllabusOutlineParseResult
        {
            IsConfidentStructure = true,
            Modules = modules
        };
    }

    /// <summary>
    /// Detects the SAME "N.0"/"N.M" bare-numeric convention as <see
    /// cref="ParseBareNumericHeadings"/>, but for documents whose text
    /// extraction collapsed ALL line breaks — a real-world PDF-to-text
    /// extraction (2026-08-09 regression) can flatten an entire page (its
    /// own Table of Contents AND the body content that follows) into one
    /// continuous block of prose with no newlines at all, so headings
    /// never start their own line and <see cref="ParseBareNumericHeadings"/>'s
    /// per-line "^...$" anchors can never match. Instead of scanning
    /// lines, this scans the WHOLE text for the document's own dot-leader
    /// Table of Contents entries (e.g. "1.1 Purpose of the Policy
    /// .......... 1") — unambiguous even with zero surrounding line
    /// structure — then locates each heading's own (later, real)
    /// occurrence in the body text by an exact substring search, mirroring
    /// the "TOC entry now, real heading later" precedence <see
    /// cref="ParseBareNumericHeadings"/> already applies for line-based
    /// documents.
    /// </summary>
    private static SyllabusOutlineParseResult ParseFlatDotLeaderToc(string documentText)
    {
        var tocMatches = FlatTocEntryRegex.Matches(documentText);
        if (tocMatches.Count < MinModules)
        {
            return Rejected("No se detectó un índice (tabla de contenidos) con numeración y puntos de relleno.");
        }

        var firstSeenOffset = tocMatches[0].Index;
        var searchFrom = tocMatches[^1].Index + tocMatches[^1].Length;

        var modules = new List<SyllabusModule>();
        var moduleByMajor = new Dictionary<int, SyllabusModule>();

        // PASS 1 — same fix/rationale as ParseBareNumericHeadings above:
        // register exactly one module object per distinct major BEFORE any
        // subsection can be attached, so a subsection whose own body
        // occurrence is located out of numeric order (e.g. a sidebar/
        // margin paragraph extracted at the wrong text position) can never
        // orphan itself on a throwaway placeholder abandoned once the
        // chapter's real "X.0" TOC entry is processed later.
        foreach (Match tocMatch in tocMatches)
        {
            if (!int.TryParse(tocMatch.Groups["major"].Value, out var majorNumber))
            {
                continue;
            }

            if (moduleByMajor.ContainsKey(majorNumber))
            {
                continue;
            }

            var module = new SyllabusModule { Number = majorNumber, Keyword = string.Empty, Title = $"{majorNumber}.0" };
            modules.Add(module);
            moduleByMajor[majorNumber] = module;
        }

        // PASS 2: locate each heading's real body occurrence (same
        // monotonic forward-search cursor as before) and attach it to the
        // SAME module object registered above, never a duplicate.
        var majorsWithOwnOffset = new HashSet<int>();
        foreach (Match tocMatch in tocMatches)
        {
            if (!int.TryParse(tocMatch.Groups["major"].Value, out var major) ||
                !int.TryParse(tocMatch.Groups["minor"].Value, out var minor))
            {
                continue;
            }

            var title = CollapseInternalWhitespace(tocMatch.Groups["title"].Value);
            var headingText = $"{major}.{minor} {title}";
            var bodyIndex = documentText.IndexOf(headingText, searchFrom, StringComparison.Ordinal);
            if (bodyIndex < 0)
            {
                return Rejected(
                    $"El índice detectado menciona '{headingText}' pero ese encabezado no vuelve a " +
                    "aparecer en el cuerpo del documento.");
            }

            searchFrom = bodyIndex + headingText.Length;

            var module = moduleByMajor[major];
            if (minor == 0)
            {
                module.Title = $"{major}.0 {title}";
                module.StartOffset = bodyIndex;
                majorsWithOwnOffset.Add(major);
                continue;
            }

            if (majorsWithOwnOffset.Add(major))
            {
                // No "X.0" heading exists for this major — fall back to
                // this (earliest-processed) subsection's own offset as the
                // chapter's start.
                module.StartOffset = bodyIndex;
            }

            module.Subsections.Add(new SyllabusSubsection
            {
                Number = $"{major}.{minor}",
                Title = title,
                StartOffset = bodyIndex
            });
        }

        modules = [.. modules.OrderBy(m => m.StartOffset)];

        if (modules.Count < MinModules)
        {
            return Rejected(
                $"Solo se detectaron {modules.Count} capítulo(s) numerado(s) en el índice — se requieren al menos " +
                $"{MinModules}.");
        }

        for (var i = 1; i < modules.Count; i++)
        {
            if (modules[i].Number <= modules[i - 1].Number)
            {
                return Rejected(
                    $"La numeración del índice no es consistente/creciente (Capítulo {modules[i - 1].Number} " +
                    $"seguido de Capítulo {modules[i].Number}).");
            }
        }

        var coverage = 1.0 - (double)firstSeenOffset / documentText.Length;
        if (coverage < MinLeadingTextCoverage)
        {
            return Rejected(
                $"El índice detectado aparece muy tarde en el documento (cobertura {coverage:P0}).");
        }

        return new SyllabusOutlineParseResult
        {
            IsConfidentStructure = true,
            Modules = modules
        };
    }

    private static SyllabusOutlineParseResult Rejected(string reason) => new()
    {
        IsConfidentStructure = false,
        RejectionReason = reason
    };
}
