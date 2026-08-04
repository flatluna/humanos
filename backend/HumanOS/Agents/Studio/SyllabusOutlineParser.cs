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
    /// detected pattern doesn't clear every quality gate below.
    /// </summary>
    public static SyllabusOutlineParseResult Parse(string documentText)
    {
        if (string.IsNullOrWhiteSpace(documentText))
        {
            return Rejected("El documento está vacío.");
        }

        var lines = documentText.Replace("\r\n", "\n").Split('\n');
        var modules = new List<SyllabusModule>();
        SyllabusModule? currentModule = null;
        var offset = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            var moduleMatch = trimmed.Length > 0 ? ModuleHeaderRegex.Match(trimmed) : Match.Empty;
            if (moduleMatch.Success && int.TryParse(moduleMatch.Groups["number"].Value, out var moduleNumber))
            {
                currentModule = new SyllabusModule
                {
                    Number = moduleNumber,
                    Keyword = moduleMatch.Groups["keyword"].Value.Trim(),
                    Title = moduleMatch.Groups["title"].Value.Trim(),
                    StartOffset = offset
                };
                modules.Add(currentModule);
                offset += line.Length + 1;
                continue;
            }

            var subsectionMatch = currentModule is not null && trimmed.Length > 0
                ? SubsectionRegex.Match(trimmed)
                : Match.Empty;
            if (subsectionMatch.Success &&
                subsectionMatch.Groups["major"].Value == currentModule!.Number.ToString())
            {
                currentModule.Subsections.Add(new SyllabusSubsection
                {
                    Number = $"{subsectionMatch.Groups["major"].Value}.{subsectionMatch.Groups["minor"].Value}",
                    Title = subsectionMatch.Groups["title"].Value.Trim(),
                    StartOffset = offset
                });
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

    private static SyllabusOutlineParseResult Rejected(string reason) => new()
    {
        IsConfidentStructure = false,
        RejectionReason = reason
    };
}
