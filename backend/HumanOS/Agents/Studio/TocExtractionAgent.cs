using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
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
        its content silently gets lost/compressed away.

        Follow this exact TWO-PHASE algorithm, in order:

        PHASE 1 — LOOK FOR THE DOCUMENT'S OWN INDEX/AGENDA FIRST.
        Many course/tutorial documents open with their own listing of
        what's covered — a table of contents, an "Índice", a "Contenido",
        an "Agenda" or "Module Agenda", or simply a short numbered list of
        section titles with little/no prose between them, appearing near
        the start of the document. If you find one:
        - Treat that listing as the AUTHORITATIVE, COMPLETE list of real
          chapters — use EXACTLY those titles, in that exact order, don't
          add ones it doesn't mention and don't skip ones it does.
        - The listing itself is NEVER a chapter boundary and NEVER
          returned as its own chapter — for each title it names, find that
          SAME title's OTHER occurrence further down in the document (the
          one immediately followed by substantive body text, not another
          bare listing) and use THAT occurrence's text as the StartMarker.
        - If a listed title never reoccurs anywhere else in the document
          text, skip that phase entirely and fall back to PHASE 2 instead
          (the index may be inaccurate or the extracted text may have
          dropped a heading) — never invent a StartMarker that doesn't
          appear verbatim in the source.

        PHASE 2 — NO USABLE INDEX/AGENDA FOUND: infer chapter boundaries
        yourself from the document's own topic shifts. Many real course/
        tutorial documents do NOT use formal "Chapter 1" / "Capítulo 1"
        labels — they're organized instead by topic headings, numbered
        sections, bullet-style subtopics, or just visible shifts in
        subject matter as the text progresses. Treat each clearly distinct
        topic/section/module as its own "chapter" for this purpose, even
        if the source never uses the word "chapter" at all. As a rule of
        thumb: any document long enough to span multiple distinct topics
        (roughly every 1-3 pages worth of text, or wherever the subject
        visibly changes) should be split at each of those boundaries, not
        treated as one block. When in doubt in this phase, prefer MORE
        chapters over fewer. Only return exactly ONE chapter (whose
        StartMarker is taken from the very beginning of the text) if the
        document is GENUINELY short or is a single cohesive piece with no
        internal topic shifts at all (e.g. one short article, one short
        note, a single narrow how-to). Do not default to one chapter just
        because there are no explicit chapter-style headings — look at
        actual topic boundaries instead.

        For each returned chapter, provide:
        - Title: the chapter's real title as it appears in the source (or
          a short descriptive title if the document has no explicit
          titles).
        - StartMarker: a short (8-15 words), EXACT, VERBATIM copy of text
          taken directly from the source, copied character-for-character
          from the point where that chapter begins. This is used to
          locate the chapter boundary via exact string search, so it MUST
          match the source text exactly (same spelling, spacing,
          punctuation) — never paraphrase or summarize it.

        WATCH FOR TOPIC HEADINGS GLUED TO SURROUNDING TEXT WITH NO
        WHITESPACE: real PDF text extraction commonly collapses ALL line
        breaks and spacing, so a running page header/footer (e.g. a page
        number immediately followed by the repeated document/guide title)
        gets glued directly onto the very next topic heading, which is in
        turn glued directly onto its own body prose — e.g.
        "...12Azure APIs+ Microservices GuideAPIs break down silosOrganizations
        continue to struggle..." actually contains a page-footer stamp
        ("12Azure APIs+ Microservices Guide"), a genuine NEW topic heading
        ("APIs break down silos"), and that topic's body prose, all with
        zero separating characters. Never let this glued-together
        appearance make you treat the whole run-on span as one topic, and
        never mistake the repeated page-footer stamp itself for a topic
        heading — read past it to find the real heading text that follows
        it, then split there. A short, title-like phrase (often just a few
        words, no punctuation) sitting between two clear shifts in subject
        matter is a topic heading even with no visible whitespace/newline
        around it and even if it never appears with any special formatting
        anywhere in the extracted text.

        NEVER A DUPLICATE ENTRY: Each distinct chapter/subsection title
        must appear EXACTLY ONCE in your returned list — if a title seems
        to occur more than once in the source, it is almost always the
        index/agenda listing (Phase 1) plus the real heading; return only
        the real one, per Phase 1's rule above.

        SKIP NAVIGATIONAL / SCAFFOLDING HEADERS — NEVER THEIR OWN CHAPTER
        Slide decks, training guides and student workbooks commonly wrap
        real content in short administrative/navigational headers that
        are NOT topics of their own — e.g. "Module Objective", "Module
        Agenda", "Practice Activity", "Knowledge Check", "Key Takeaway",
        "Source note", "References", "Summary", "Introduction" (when it's
        just a one-line stub before the real first topic). Never return
        one of these as its own chapter — fold the text under it into
        whichever real, substantive topic/section immediately precedes or
        follows it instead.

        THIS RULE NEVER APPLIES TO A NUMBERED/NAMED TOPIC SECTION ITSELF:
        anything titled like "Section 1 | ...", "Section 2 | ...", "Unit 3:
        ...", "Chapter 4 — ...", etc. is ALWAYS its own separate, mandatory
        chapter boundary — it is NEVER a "navigational header" and must
        NEVER be folded into a neighboring section or merged with any
        other numbered section, no matter how short its own content is or
        how document-wide the overall topic feels. A document with
        "Section 1 | Change Is Individual" through "Section 4 | Applying
        ADKAR With Others" MUST return (at least) 4 separate chapters, one
        per numbered section — collapsing them into a single chapter
        because the whole module shares one overarching subject (e.g.
        "ADKAR") is WRONG. Only genuinely unnumbered, purely
        administrative headers (the list above) get folded — every
        numbered/named content section is mandatory and independent.

        DO NOT FRAGMENT ONE SECTION'S OWN ENUMERATED LIST INTO SEPARATE
        CHAPTERS: when a single section/topic explains a model, framework,
        or process by walking through its own components or steps one at a
        time (e.g. a "AWARENESS / DESIRE / KNOWLEDGE / ABILITY /
        REINFORCEMENT" breakdown of one named model, or "1. Listen / 2.
        Diagnose / 3. Respond" steps of one procedure), those components
        stay INSIDE that same numbered section's own chapter, never one
        chapter each and never merged into a DIFFERENT numbered section.
        Only split a component out as its own chapter if it independently
        spans multiple paragraphs of genuinely distinct, freestanding
        content comparable in depth to a real topic section elsewhere in
        the document — a short label plus a one-line definition (typical
        of a table row or bullet) must stay merged into its own parent
        section only.
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

        STRICT SYLLABUS MODE — ACTIVE FOR THIS DOCUMENT: do NOT stop at the
        top-level chapter/unit headings — you MUST also detect every finer
        subsection/topic heading nested under each chapter as its OWN
        separate chapter entry, in the exact same order they appear. This
        applies regardless of whether the subsections use formal numbering
        (e.g. "1.0", "1.1", "1.2"...) or are just distinct named topic
        headings with no numbering at all (e.g. under a "CHAPTER ONE —
        What are web APIs?" heading, narrower headings like "The growth of
        web and mobile APIs", "APIs as capabilities", "API business
        models" are each their own subsection, not filler text):
        - One chapter entry per subsection/topic heading, never one entry
          per top-level unit covering several subsections at once.
        - Include every subsection, even short/procedural ones that might
          otherwise seem like scaffolding (e.g. "Introducción, competencias
          y objetivos", "Actividad complementaria", "Criterios de
          evaluación") — each one is its own mandatory chapter, never
          merged into a neighboring subsection or dropped.
        - Title each detected chapter after that subsection's own heading
          text (numbering may be included or dropped, your choice, but the
          Title must clearly correspond to exactly that one subsection).
        - This applies uniformly to every unit/section/chapter in the
          document, not just the first one. If the document uses numbered
          subsections, before finishing re-scan your own numbering
          sequence end-to-end (1.1, 1.2, 1.3, 2.1, 2.2, ...) and confirm no
          number is missing — a gap almost always means you skipped a real
          subsection rather than the document actually lacking one.
        - Set ParentModuleTitle on EVERY subsection/topic-level chapter
          entry to that subsection's own parent chapter/Unit/Module's
          heading text (e.g. "CHAPTER ONE — What are web APIs?" or
          "Introducción a la Álgebra"), exactly as it appears in the
          source — this groups subsections back under their shared level.
          Leave ParentModuleTitle null ONLY for a genuine top-level chapter
          heading that has no such parent (e.g. this document turns out to
          have no real 2-level nesting at all).

        DO NOT INVENT A FAKE PARENT MODULE FROM THE DOCUMENT'S OWN TITLE:
        a document's own overall title/cover heading (e.g. "ADKAR
        Foundations | Student Learning Guide") is NEVER a parent
        module/unit that its numbered/named sections nest under — it is
        just the document's title, not a chapter level. If the document
        only has ONE tier of numbered/named sections (e.g. "Section 1 |
        ...", "Section 2 | ...", ... with no further numbered subsections
        like "1.1", "1.2" inside them), there is NO real 2-level nesting
        at all: leave ParentModuleTitle null on EVERY one of those
        sections and return each as its own independent top-level
        chapter. Only set ParentModuleTitle when the source itself
        genuinely nests distinct numbered/named subsections beneath a
        distinct numbered/named parent unit — never to group otherwise
        top-level sections under the document's own subject/title just
        because they all belong to the same overall topic.
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
        - Do NOT propose one node per item of a single enumerated
          model/framework/process (e.g. each letter of an acronym like
          Awareness/Desire/Knowledge/Ability/Reinforcement, or each
          numbered step of one procedure) — those belong together as ONE
          node covering that whole model/process, not one node per item.
        - Never propose a chapter or node for a purely navigational/
          administrative header with no real topic of its own (e.g.
          "Agenda", "Module Objective", "Practice Activity", "Knowledge
          Check", "Key Takeaway", "Source note") — fold that text into the
          real topic node it belongs to instead.
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
    private readonly ChatClientAgentRunOptions? _extractRunOptions;
    private readonly ChatClientAgentRunOptions? _inferOutlineRunOptions;

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
        _extractRunOptions = BuildDeterministicRunOptions(deploymentName);

        // Main (non-economy) deployment for inference — this call requires
        // genuine reading comprehension/judgment (proposing a pedagogical
        // breakdown), not mechanical boundary detection.
        var inferDeploymentName = configuration["AzureOpenAIDeploymentName"];
        _inferOutlineAgent = string.IsNullOrWhiteSpace(inferDeploymentName)
            ? null
            : client.GetChatClient(inferDeploymentName).AsAIAgent(instructions: InferOutlineInstructions, name: "TocInferOutlineAgent");
        _inferOutlineRunOptions = string.IsNullOrWhiteSpace(inferDeploymentName) ? null : BuildDeterministicRunOptions(inferDeploymentName);
    }

    // Boundary detection is a reading-comprehension/pattern-matching task with
    // one correct answer, not a creative one — temperature 0 makes the same
    // document split the same way every run instead of the granularity
    // silently varying between otherwise-identical requests. Reasoning models
    // (e.g. gpt-5, o1/o3) reject the Temperature parameter outright (500 from
    // the API), so it's only set for non-reasoning deployments.
    private static ChatClientAgentRunOptions? BuildDeterministicRunOptions(string deploymentName)
    {
        var isReasoningModel = deploymentName.Contains("gpt-5", StringComparison.OrdinalIgnoreCase)
            || deploymentName.Contains("o1", StringComparison.OrdinalIgnoreCase)
            || deploymentName.Contains("o3", StringComparison.OrdinalIgnoreCase);
        if (isReasoningModel)
        {
            return null;
        }

        return new ChatClientAgentRunOptions(new ChatOptions
        {
#pragma warning disable OPENAI001
            RawRepresentationFactory = _ => new ChatCompletionOptions { Temperature = 0f }
#pragma warning restore OPENAI001
        });
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
        var response = await _inferOutlineAgent.RunAsync<InferredOutline>(prompt, options: _inferOutlineRunOptions, cancellationToken: cancellationToken);
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

        var response = await _agent.RunAsync<TableOfContents>(prompt, options: _extractRunOptions, cancellationToken: cancellationToken);

        return response.Result;
    }
}
