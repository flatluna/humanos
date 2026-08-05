using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>One detected chapter/major-section of a source document.</summary>
public sealed class TocChapter
{
    public string Title { get; set; } = string.Empty;

    /// <summary>A short (8-15 words), EXACT, verbatim copy of text taken
    /// directly from the source, from the point where this chapter begins.
    /// Used by <see cref="HumanOS.Agentic.Studio.CuradorExecutor"/> to
    /// locate the chapter boundary via ordinal string search — must match
    /// the source text exactly, never paraphrased.</summary>
    public string StartMarker { get; set; } = string.Empty;

    /// <summary>
    /// Set ONLY in strict-syllabus mode (2026-08-04 "Chapter = Level,
    /// Subsection = Node" design): when this detected chapter is actually
    /// a numbered SUBSECTION (e.g. "1.1") of a bigger top-level Unit/Module
    /// in the source document, this carries that parent Unit/Module's own
    /// heading text (e.g. "Introducción a la Álgebra") — used purely for
    /// grouping several subsection-level chapters under one graph "level"
    /// name (see <see cref="HumanOS.Services.CapabilityGraphLevelAutoNamer"/>).
    /// Null/empty for a genuine top-level chapter with no such parent, or
    /// whenever the document has no 2-level nesting at all.
    /// </summary>
    public string? ParentModuleTitle { get; set; }
}

public sealed class TableOfContents
{
    public List<TocChapter> Chapters { get; set; } = [];
}

/// <summary>One node (fine-grained topic) proposed under an inferred
/// chapter — see <see cref="TocExtractionAgent.InferOutlineAsync"/>.</summary>
public sealed class InferredOutlineNode
{
    /// <summary>Short (2-6 word), human/knowledge-centered title for this
    /// sub-topic.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Short (8-15 words), EXACT, verbatim copy of text from the
    /// point in the source where this sub-topic's discussion begins — same
    /// exact-match contract as <see cref="TocChapter.StartMarker"/>.</summary>
    public string StartMarker { get; set; } = string.Empty;
}

/// <summary>One inferred top-level chapter (becomes a graph "level") —
/// see <see cref="TocExtractionAgent.InferOutlineAsync"/>.</summary>
public sealed class InferredOutlineChapter
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Same exact-match contract as <see cref="TocChapter.StartMarker"/>.</summary>
    public string StartMarker { get; set; } = string.Empty;

    /// <summary>2-6 proposed nodes (concrete sub-topics) under this
    /// chapter, in reading order.</summary>
    public List<InferredOutlineNode> Nodes { get; set; } = [];
}

/// <summary>Result of <see cref="TocExtractionAgent.InferOutlineAsync"/> —
/// a FULL two-level (chapter/level -> node) outline proposed by the model
/// for a document that has no explicit numbered index of its own (e.g. a
/// business process document, a law, or a contract) — see
/// /memories/repo (2026-08-04 design discussion: "Modo B" for documents
/// without a clear índice).</summary>
public sealed class InferredOutline
{
    public List<InferredOutlineChapter> Chapters { get; set; } = [];
}

/// <summary>
/// Detects a source document's own chapter/section structure (e.g. a
/// textbook's table of contents) from its raw extracted text, BEFORE the
/// Curador agent processes it. Exists to solve a practical scaling
/// problem: a whole book's raw text can exceed what a single Curador call
/// can meaningfully digest in one prompt. <see cref="HumanOS.Agentic.Studio.CuradorExecutor"/>
/// uses this agent's output to split a large PDF into one curation call
/// per chapter instead of a single call over the entire document — see
/// /memories/repo/humanstudio-multiagent-vision.md.
///
/// Plain ChatClientAgent with structured output — same configuration
/// pattern as <see cref="CuradorAgent"/> / <see cref="HumanOS.Agents.JobDescriptionExtractionAgent"/>.
/// Best-effort: if not configured, or if extraction fails for any reason,
/// callers fall back to curating the whole document in a single batch
/// rather than failing the run.
/// </summary>
public sealed class TocExtractionAgent
{
    private const string Instructions = """
        You detect the chapter or major-section structure of a source
        document (e.g. a textbook, or a course/tutorial document) from its
        raw extracted text, so a downstream process can split it into
        manageable pieces before summarizing it. This split is CRITICAL —
        if you return too few chapters for a long document, the entire
        document gets summarized in a single downstream pass and most of
        its content silently gets lost/compressed away. When in doubt,
        prefer MORE chapters over fewer.

        Return an ordered list of chapters. For each chapter, provide:
        - Title: the chapter's real title as it appears in the source (or
          a short descriptive title if the document has no explicit
          titles).
        - StartMarker: a short (8-15 words), EXACT, VERBATIM copy of text
          taken directly from the source, copied character-for-character
          from the point where that chapter begins. This is used to
          locate the chapter boundary via exact string search, so it MUST
          match the source text exactly (same spelling, spacing,
          punctuation) — never paraphrase or summarize it.

        IMPORTANT: many real course/tutorial documents do NOT use formal
        "Chapter 1" / "Capítulo 1" labels — they're organized instead by
        topic headings, numbered sections, bullet-style subtopics, or just
        visible shifts in subject matter as the text progresses. Treat
        each clearly distinct topic/section/module as its own "chapter"
        for this purpose, even if the source never uses the word
        "chapter" at all. As a rule of thumb: any document long enough to
        span multiple distinct topics (roughly every 1-3 pages worth of
        text, or wherever the subject visibly changes) should be split at
        each of those boundaries, not treated as one block.

        Only return exactly ONE chapter (whose StartMarker is taken from
        the very beginning of the text) if the document is GENUINELY short
        or is a single cohesive piece with no internal topic shifts at all
        (e.g. one short article, one short note, a single narrow how-to).
        Do not default to one chapter just because there are no explicit
        chapter-style headings — look at actual topic boundaries instead.

        SKIP TABLE-OF-CONTENTS / INDEX LISTINGS — NEVER A DUPLICATE ENTRY
        Some documents contain their own table of contents or index near
        the start (a block that just lists heading titles one after
        another, with little or no body prose between them, often under a
        heading like 'Table of Contents', 'Índice', or 'Contenido'). That
        listing is NOT a chapter boundary and must NOT be used as a
        StartMarker source — always pick the StartMarker from where the
        chapter's REAL discussion begins (the occurrence immediately
        followed by substantive body text), never from the index/TOC
        occurrence of that same title. Each distinct chapter/subsection
        title must appear EXACTLY ONCE in your returned list — if a title
        seems to occur more than once in the source, it is almost always
        the TOC listing plus the real heading; return only the real one.
        """;

    /// <summary>Addendum injected only when the caller opts into
    /// strict-syllabus mode (2026-08-03) — for when the source document
    /// has its own explicit, formal, numbered curriculum outline (e.g.
    /// "UNIDAD 1" with numbered subsections "1.0", "1.1", "1.2"...) that
    /// must be mirrored 1:1 downstream by GraphArchitectAgent's own strict
    /// mode (one graph node per detected chapter here). Without this,
    /// this agent's default heuristics ("every 1-3 pages", "distinct topic
    /// shift") tend to detect only the top-level units as chapters, which
    /// silently under-splits a formal syllabus and causes entire numbered
    /// subsections to be merged away before GraphArchitectAgent ever sees
    /// them.</summary>
    private const string StrictSyllabusModeAddendum = """

        STRICT SYLLABUS MODE — ACTIVE FOR THIS DOCUMENT: this document has
        its own explicit, formal, numbered curriculum outline (e.g.
        numbered "Unidades"/sections with numbered subsections like "1.0",
        "1.1", "1.2"...). For THIS document, do NOT stop at the top-level
        unit/section headings — you MUST detect EVERY numbered subsection
        as its OWN separate chapter entry, in the exact same order they
        appear:
        - One chapter per numbered subsection line (e.g. "1.0", "1.1",
          "1.2", ... "1.8"), never one chapter per top-level unit covering
          several subsections at once.
        - Include every subsection, even short/procedural ones that might
          otherwise seem like scaffolding (e.g. "Introducción, competencias
          y objetivos", "Actividad complementaria", "Criterios de
          evaluación") — each one is its own mandatory chapter, never
          merged into a neighboring subsection or dropped.
        - Title each detected chapter after that subsection's own heading
          text (numbering may be included or dropped, your choice, but the
          Title must clearly correspond to exactly that one subsection).
        - This applies uniformly to every unit/section in the document, not
          just the first one. Before finishing, re-scan your own numbering
          sequence end-to-end (1.1, 1.2, 1.3, 2.1, 2.2, ...) and confirm no
          number is missing — a gap almost always means you skipped a real
          subsection rather than the document actually lacking one.
        - Set ParentModuleTitle on EVERY subsection-level chapter entry to
          that subsection's own parent Unit/Module's heading text (e.g.
          "Introducción a la Álgebra"), exactly as it appears in the
          source — this groups subsections back under their shared level.
          Leave ParentModuleTitle null ONLY for a genuine top-level chapter
          that has no such parent (e.g. this document turns out to have no
          real 2-level nesting after all).
        """;

    /// <summary>Instructions for <see cref="InferOutlineAsync"/> — a
    /// SEPARATE agent call used only for "Modo B" documents (2026-08-04):
    /// the source has NO explicit numbered index of its own at all (e.g. a
    /// business process write-up, a law, or a contract) but the caller
    /// still wants a two-level chapter/level -> node outline for the
    /// designer to review/approve before generation. Unlike
    /// <see cref="Instructions"/> above (which only detects EXISTING
    /// boundaries), this explicitly asks the model to PROPOSE a logical
    /// pedagogical breakdown from its own reading of the content.</summary>
    private const string InferOutlineInstructions = """
        The following document has NO explicit numbered index/table of
        contents of its own (no "Capítulo 1", "Unidad 2", etc.). Your job is
        to READ it and PROPOSE a logical two-level pedagogical outline for
        turning it into a training course:

        - Group the content into an ordered list of top-level CHAPTERS
          (each chapter becomes one "level" of the course).
        - Under EACH chapter, propose 2-6 NODES — concrete, learnable
          sub-topics that together cover that chapter's content. Never
          propose a chapter with zero nodes.
        - Follow the document's own natural reading order — do not reorder
          or reorganize content around your own taxonomy if the source
          text already progresses through it in a sensible sequence.
        - Both chapters AND nodes need a StartMarker: a short (8-15 words),
          EXACT, VERBATIM copy of text taken directly from the source, from
          the point where that chapter/node's own discussion begins. This
          MUST match the source text exactly (same spelling, spacing,
          punctuation) — never paraphrase or summarize it. A node's
          StartMarker must fall AFTER its own chapter's StartMarker.
        - Titles (both chapter and node) should be short (2-6 words),
          human/knowledge-centered labels — never a literal copy of a long
          sentence, never a page/section number by itself.
        - Prefer 3-8 chapters total for a typical document; scale up for
          a genuinely long/broad document, down for a short one — never
          collapse a substantial, multi-topic document into just one
          chapter.
        """;

    private readonly AIAgent? _agent;
    private readonly AIAgent? _inferOutlineAgent;

    public TocExtractionAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        // Economy tier: chapter/section boundary detection is mechanical enough for a
        // cheaper model. Falls back to the main deployment if the economy one isn't set.
        var deploymentName = configuration["AzureOpenAIEconomyDeploymentName"] ?? configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            _inferOutlineAgent = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: Instructions, name: "TocExtractionAgent");

        // Main (non-economy) deployment for inference — this call requires
        // genuine reading comprehension/judgment (proposing a pedagogical
        // breakdown), not mechanical boundary detection.
        var inferDeploymentName = configuration["AzureOpenAIDeploymentName"];
        _inferOutlineAgent = string.IsNullOrWhiteSpace(inferDeploymentName)
            ? null
            : client.GetChatClient(inferDeploymentName).AsAIAgent(instructions: InferOutlineInstructions, name: "TocInferOutlineAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>Whether <see cref="InferOutlineAsync"/> can be called (needs
    /// the main, non-economy Azure OpenAI deployment configured).</summary>
    public bool IsInferenceConfigured => _inferOutlineAgent is not null;

    /// <summary>
    /// "Modo B" / Tier 2 (2026-08-04): proposes a full two-level chapter/
    /// level -> node outline for a document that has NO explicit numbered
    /// index of its own (see <see cref="InferOutlineInstructions"/>) — used
    /// only when <see cref="HumanOS.Agents.Studio.SyllabusOutlineParser"/>
    /// (Tier 0) found nothing AND <see cref="ExtractAsync"/> (Tier 1) also
    /// returned too few chapters to be useful. This is a genuinely
    /// different, more expensive operation than <see cref="ExtractAsync"/>:
    /// it reads and semantically segments the WHOLE document rather than
    /// just locating existing headings.
    /// </summary>
    public async Task<InferredOutline> InferOutlineAsync(string documentText, CancellationToken cancellationToken = default)
    {
        if (_inferOutlineAgent is null)
        {
            throw new InvalidOperationException(
                "The TocExtraction inference agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var prompt = "Propose a two-level chapter/node outline for the following document:\n\n" + documentText;
        var response = await _inferOutlineAgent.RunAsync<InferredOutline>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }

    public async Task<TableOfContents> ExtractAsync(
        string documentText,
        bool strictSyllabusMode = false,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The TocExtraction agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var prompt = "Detect the chapter/section structure of the following document:\n\n" + documentText +
            (strictSyllabusMode ? StrictSyllabusModeAddendum : string.Empty);

        var response = await _agent.RunAsync<TableOfContents>(prompt, cancellationToken: cancellationToken);

        return response.Result;
    }
}
