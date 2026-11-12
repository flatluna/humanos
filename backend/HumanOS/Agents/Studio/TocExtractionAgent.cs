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

/// <summary>One ENGRAM (fine-grained, self-contained teachable chunk) under
/// a <see cref="DirectChapter"/> — produced ONLY by
/// <see cref="TocExtractionAgent.ExtractDirectAsync"/>. Unlike
/// <see cref="TocChapter"/>/<see cref="InferredOutlineNode"/>, this carries
/// its OWN verbatim <see cref="Content"/> directly — no separate
/// StartMarker + ordinal-search slicing step is needed downstream.</summary>
public sealed class DirectEngram
{
    /// <summary>Short (2-6 word), human/knowledge-centered title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>One-sentence capability this engram builds in the learner
    /// — same role as <see cref="HumanOS.Models.Capabilities.Studio.StructuredLearningDocumentSection.Capability"/>.</summary>
    public string Capability { get; set; } = string.Empty;

    /// <summary>EXACT, VERBATIM text copied from the source document for
    /// this engram — never paraphrased, never summarized, never invented.
    /// Only the BOUNDARIES (where one engram ends and the next begins) are
    /// the model's own judgment; the characters inside must be an exact
    /// substring of the source.</summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>One top-level CHAPTER produced by
/// <see cref="TocExtractionAgent.ExtractDirectAsync"/> — groups 2+ engrams
/// that together cover one real topic/section of the source document.</summary>
public sealed class DirectChapter
{
    /// <summary>The chapter's REAL heading, copied as literally as
    /// possible from the source (never shortened/paraphrased) when the
    /// document has an explicit heading at that point. Only a genuinely
    /// descriptive title of your own may be used when NO explicit heading
    /// exists in the source at all (pure topic-shift inference).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>One or two sentence generic description of what this
    /// chapter covers — shown to the designer in the approval UI.</summary>
    public string Description { get; set; } = string.Empty;

    public List<DirectEngram> Engrams { get; set; } = [];
}

/// <summary>Result of <see cref="TocExtractionAgent.ExtractDirectAsync"/> —
/// a FULL Chapter -&gt; Engram breakdown WITH each engram's own verbatim
/// content already attached, produced in a SINGLE call (2026-10-29 design:
/// replaces the detect-boundary-then-slice-by-StartMarker approach for
/// documents small enough to fit in one context window — see
/// /memories/repo/structuredlearning-direct-chunking.md). Eliminates the
/// StartMarker/ordinal-search step entirely (and the class of bugs where a
/// misidentified "heading" — e.g. a bolded lead-in sentence to a bullet
/// list — silently produces a valid-but-wrong chapter boundary) by having
/// the model hand back the already-sliced content directly instead of
/// just a short marker for code to re-locate later.</summary>
public sealed class DirectExtraction
{
    /// <summary>The document's own cover/title heading, copied VERBATIM —
    /// null/empty if the source has no distinct title line of its own
    /// (e.g. it starts directly with body content).</summary>
    public string? DocumentTitle { get; set; }

    /// <summary>The document's own subtitle/tagline immediately under its
    /// title, copied VERBATIM — null/empty if none exists.</summary>
    public string? DocumentSubtitle { get; set; }

    public List<DirectChapter> Chapters { get; set; } = [];
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
          titles). NEVER use a "[Descripción de imagen — página N]"
          marker (internal scaffolding inserted by the extraction
          pipeline, not real document text) as or inside a Title, even
          when it is the very first text of that chapter — write your own
          short descriptive title for that chapter instead.
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
        - Include every subsection that teaches something, even short/
          procedural ones that might otherwise seem like scaffolding (e.g.
          "Introducción, competencias y objetivos") — each one is its own
          mandatory chapter, never merged into a neighboring subsection or
          dropped.
        - EXCEPTION — NEVER A SUBSECTION OF ITS OWN: an
          assessment/practice/review heading whose job is to VERIFY or
          REINFORCE knowledge the learner is assumed to already have from
          the subsection right before it, rather than teach anything new
          — e.g. "Knowledge Check", "Pause and Explain", "Diagnostic
          Activity", "In-Class Learning Activity", "Applied Challenge",
          "PROFE recap", "Oral Defense Questions", "Practice Activity",
          "Review Questions", "Quiz", "Self-Check", "Criterios de
          evaluación", "Actividad complementaria". This rule applies even
          in strict syllabus mode and even when such a heading is numbered
          like a real subsection — fold its text into the PRECEDING real,
          substantive subsection instead of giving it its own chapter
          entry. A long list of review questions is still not new content.
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
        - IMAGE-ONLY INPUT: if the source text consists entirely (or
          almost entirely) of blocks marked "[Descripción de imagen —
          página N]" with no real surrounding body text of its own — i.e.
          the document is just scanned/pasted images with no actual
          prose — treat EACH distinct image-description block as exactly
          ONE chapter (never split a single image's own description
          across several chapters, and never merge two different images'
          blocks into one chapter). Title that chapter after what the
          image actually depicts (its own title/heading, e.g. from a
          "# Título" line in its description) — never a generic label
          like "Page 1" or "Image 1". WITHIN that one chapter, propose one
          node per real section of the image's own index/structure (e.g.
          its "# Índice de Contenidos" headings/numbered items such as
          "1. Change Narrative", "2. Culture", etc.) — each such node's
          content is that section's own transcribed text (title + body),
          not the whole image description repeated. If the image's
          description has an executive-summary section (e.g. "# Resumen
          Ejecutivo"), make that the chapter's own first node/intro
          instead of a separate index item. This overrides the general
          "3-8 chapters total" guidance below (one image = one chapter,
          however many chapters that makes), but the normal "2-6 nodes
          per chapter" guidance still applies to how that chapter's own
          index sections are split into nodes.
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

    /// <summary>Instructions for <see cref="ExtractDirectAsync"/> (2026-10-29)
    /// — used only for documents small enough to fit entirely in one call
    /// (see <see cref="HumanOS.Services.CapabilityOutlinePreviewService"/>'s
    /// own size threshold). Unlike <see cref="Instructions"/>/
    /// <see cref="InferOutlineInstructions"/>, which only locate/propose a
    /// SHORT StartMarker for code to re-locate and slice afterwards, this
    /// has the model hand back each engram's own full VERBATIM content
    /// directly — removing the separate "locate this short marker again,
    /// exactly, inside the raw text" step entirely, which is where subtle
    /// misclassifications (e.g. a bolded lead-in sentence to a bullet list
    /// mistaken for a new heading) used to silently produce a wrong-but-
    /// valid boundary instead of a visible failure.</summary>
    /// <summary>Instructions for <see cref="SplitModuleIntoEngramsAsync"/>
    /// (2026-11-10) — unlike <see cref="InferOutlineInstructions"/> (whole
    /// document -> chapters+nodes) or <see cref="DirectExtractionInstructions"/>
    /// (whole document -> chapters+engrams WITH content), this call is
    /// scoped to just ONE already-identified module/chapter's own text
    /// (from <see cref="HumanOS.Agents.Studio.SyllabusOutlineParser"/>'s
    /// Tier 0, when that module had no numbered/keyworded subsections of
    /// its own) — used to propose real engram-level sub-topic boundaries
    /// INSIDE it instead of leaving the whole module as one oversized
    /// single node.</summary>
    private const string EngramSplitInstructions = """
        You are given the full text of ONE module/chapter of a training
        course (its own heading already removed/implied — do not propose
        an engram for the module's own title). Your job is to split THIS
        module's own text into ENGRAMS: small, self-contained teachable
        ideas a learner can absorb and be quizzed on as one unit.

        - Only split where it genuinely makes sense: if the module already
          reads as ONE coherent idea, return exactly one engram covering
          the whole text — never force a split just to produce more than
          one result.
        - Break at genuine topic/idea boundaries — typically the module's
          own named sub-headings (e.g. "The Problem That Forced Computing
          Forward", "Were These Machines Intelligent?", "The First
          Artificial Neuron" each becoming their own engram when they are
          substantial, self-contained ideas).
        - NEVER make a standalone engram out of pure scaffolding/framing
          text with no teachable content of its own — e.g. an opening
          hook/scenario question, a "Learning Objectives" bullet list, an
          "Opening Conversation" prompt, a "Diagnostic Activity"
          instructions block, or an "Ask PROFE: ..." prompt. Fold each of
          these into the very next (or, if there is none after it, the
          previous) real content engram instead — they set up or reinforce
          that engram's idea, they are not their own idea.
        - NEVER make a standalone engram out of an ASSESSMENT/PRACTICE/
          REVIEW section either — e.g. "Knowledge Check", "In-Class
          Learning Activity", "Applied Challenge", "PROFE recap",
          "PROFE Oral Defense Questions", a quiz, a set of review
          questions, or any other block whose job is to VERIFY or
          REINFORCE knowledge the learner is assumed to already have from
          an earlier engram. An engram is a NEW, self-contained piece of
          knowledge the learner has not yet seen — a question that only
          makes sense once that knowledge already exists is not a new
          engram, no matter how long the list of questions is. Fold this
          text into the PRECEDING real content engram it checks/practices
          (never into the next one, and never on its own) — a learner
          encountering it as its own standalone unit would be confused,
          since there is nothing new being taught there.
        - Do NOT propose one engram per item of a single enumerated
          list/model/process — keep that whole list/model together as
          part of ONE engram.
        - Every word of the module's own text must end up inside exactly
          one engram's span — do not silently drop any part of it (the
          scaffolding text above still belongs somewhere, just not alone).
        - Each engram needs a Title (2-6 words, human/knowledge-centered)
          and a StartMarker: a short (8-15 words), EXACT, VERBATIM copy of
          text from the point in THIS module's text where that engram
          begins (same spelling/spacing/punctuation as the source — never
          paraphrased). The very first engram's StartMarker must be the
          module's own very first line of real text.
        - Return engrams in reading order.
        """;

    private const string DirectExtractionInstructions = """
        You split a source document (e.g. a textbook chapter, an online
        article, a course/tutorial page) into a Chapter -> Engram
        structure for a training course, reading the ENTIRE document
        yourself and returning the real content inline — not just where
        to cut it.

        FIRST, check for a cover/title heading and subtitle/tagline at the
        very start of the document (e.g. a whitepaper's own title line
        followed by a one-line tagline) and copy BOTH verbatim into
        DocumentTitle/DocumentSubtitle if present — these are NEVER
        returned as their own chapter.

        An "Engram" is ONE small, self-contained teachable idea: a single
        concept, fact, procedure step, or closely-related small group of
        facts that a learner can absorb and be quizzed on as one unit.
        Getting engram SIZE right matters as much as getting the topic
        boundaries right:
        - Target roughly 900-2000 characters (about 200-450 words) of
          verbatim source text per engram. This is a GUIDELINE, not a hard
          rule — never split one coherent idea into two pieces just to
          hit this range, and never force two genuinely different ideas
          together just to pad a short one up to it.
        - TOO SMALL is a real failure mode: a single bullet point, a bare
          definition with no elaboration, or one sentence fragment is not
          its own engram — fold it into the engram covering the topic it
          belongs to.
        - TOO LARGE is an equally real failure mode: if a chapter's whole
          topic is one giant wall of text, you MUST split it into several
          engrams by its own internal sub-topics/steps/examples rather
          than returning it as one oversized engram — a single engram
          holding most of the document's content is ALWAYS wrong.

        STEP 1 — DETECT TOP-LEVEL CHAPTERS.
        Identify the document's own real topic/section boundaries, same
        as detecting a table of contents: look first for the document's
        own explicit index/agenda/table of contents if it has one and use
        it as the authoritative chapter list; otherwise infer chapters
        from the document's own visible topic shifts. Prefer 3-8 chapters
        for a typical document.

        IMAGE-ONLY INPUT AT THE CHAPTER LEVEL: a block marked
        "[Descripción de imagen — página N]" with no real surrounding
        prose of its own is a whole distinct source image, not a topic to
        be merged with any other image or with nearby real text. Each
        such block is its OWN chapter — never group two different
        images' blocks into one chapter, and never fold an image's block
        into the chapter covering the real prose text around it.

        NEVER USE THE "[Descripción de imagen — página N]" MARKER ITSELF
        AS A TITLE: that bracketed line is internal scaffolding inserted
        by the extraction pipeline, NOT a heading written by the
        document's own author — it must NEVER be copied (whole or
        partial, with or without the brackets) into any chapter's or
        engram's Title, even for an image-only chapter whose content
        starts right after that marker. For an image-only chapter, derive
        the Title from the image description's OWN content instead: its
        own "# Título"/"# Resumen Ejecutivo" heading if present, or
        otherwise a short descriptive title you write yourself that
        reflects what the image actually depicts.

        CHAPTER TITLES MUST BE LITERAL, NEVER SHORTENED OR PARAPHRASED:
        if the document has a real, visible heading at a chapter's start
        (even a long one, e.g. "Decision framework: Choosing agentic vs.
        traditional automation"), copy that heading VERBATIM as the
        Title — in FULL, including any subtitle/colon clause after it.
        Do NOT compress it down to just its first few words. Only write a
        short descriptive title of your own when the source genuinely has
        NO explicit heading at that boundary at all (pure topic-shift
        inference with nothing to copy).

        STEP 2 — BREAK EACH CHAPTER INTO ENGRAMS.
        Within each chapter, in reading order, produce 2 or more engrams
        that together cover ALL of that chapter's substantive content
        (never skip or drop any body text — only navigational/
        administrative scaffolding like "Knowledge Check", "Summary",
        "References", "Module Agenda" may be omitted if it adds nothing
        pedagogically).

        IMAGE-DESCRIPTION CHAPTERS: when a chapter's own content is a
        single block marked "[Descripción de imagen — página N]" (i.e.
        this chapter IS one described image), split it into engrams by
        the image's OWN internal structure, same as any other chapter:
        - If the image description has a "# Resumen Ejecutivo" section,
          that becomes the chapter's first engram (its intro/summary).
        - Each real section of the image's own "# Índice de Contenidos"
          (its own numbered/named headings, e.g. "1. Change Narrative",
          "2. Culture") becomes its own engram, with Content equal to
          that section's own transcribed heading + body text verbatim
          — never the whole image description repeated per engram.
        - Never split ONE such section into multiple engrams, and never
          merge two different sections together into one engram.
        - Never let an engram's Content span across TWO different
        "[Descripción de imagen — página N]" blocks (whether from the
        same or different pages) — each image's own block, and all its
        engrams, must stay wholly inside its own single chapter, never
        merged with another image's block or with surrounding real prose.
        - If the image description has no internal structure at all (no
          executive summary, no index sections — just a flat
          transcription/description), fall back to exactly one engram
          for the whole chapter rather than forcing an artificial split.

        A COMMON TRAP TO AVOID: a short bolded/emphasized sentence that
        INTRODUCES a bullet list (e.g. "Key components of X include:")
        is NOT a new topic heading and NOT the start of a new engram —
        it is the opening sentence of the SAME engram as the list that
        follows it. Never treat a list's own lead-in sentence as a
        chapter/engram boundary; keep the lead-in sentence and its list
        together in one engram.

        ENGRAM TITLE IS A WARNING SIGN CHECK: if the title you're about to
        write for an engram joins two different topic names together
        (e.g. with "&", "and", "/") or ends in a word like "Start",
        "cont.", "continued", or "(cont'd)", that is a strong signal your
        slice boundary landed in the MIDDLE of a real sub-topic instead of
        at its edges. When this happens, move the boundary to the actual
        edge between the two sub-topics (even if it makes this engram or
        its neighbor smaller/larger than the usual target size) and
        rewrite the title to name the ONE coherent idea the engram
        actually covers — never ship a title that is really two topic
        names stitched together.

        PRESERVE THE DOCUMENT'S OWN ORDER EXACTLY: chapters and engrams
        must be returned in the exact same order their content appears in
        the source, start to finish — never reorder a chapter/engram to
        group similar topics together or for any other reason.

        For each engram, provide:
        - Title: short (2-6 words), human/knowledge-centered label
          naming the ONE idea this engram covers — never a literal copy
          of a long sentence, and never two topic names joined together
          (see the warning-sign check above).
        - Capability: one short sentence naming the concrete capability/
          skill this engram builds in the learner (not a restatement of
          the title).
        - Content: the VERBATIM text for this engram, copied EXACTLY
          character-for-character from the source (same spelling,
          spacing, punctuation — never paraphrase, summarize, reorder, or
          invent). You may choose where to START and STOP each slice, but
          every character you return inside it must genuinely appear in
          the source text in that order. Every character of the source's
          substantive content must end up inside EXACTLY ONE engram —
          never duplicated across two engrams, never dropped entirely.

        For each chapter, also provide a short (1-2 sentence) Description
        summarizing what it covers, for a human reviewer — this one field
        may be your own words, not verbatim.
        """;

    private readonly AIAgent? _agent;
    private readonly AIAgent? _inferOutlineAgent;
    private readonly AIAgent? _directExtractionAgent;
    private readonly AIAgent? _engramSplitAgent;
    private readonly ChatClientAgentRunOptions? _extractRunOptions;
    private readonly ChatClientAgentRunOptions? _inferOutlineRunOptions;
    private readonly ChatClientAgentRunOptions? _directExtractionRunOptions;
    private readonly ChatClientAgentRunOptions? _engramSplitRunOptions;

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

        // Same main (non-economy) deployment as inference — generating the
        // actual engram content/boundaries in one pass is a genuine reading-
        // comprehension task, not mechanical marker-finding.
        _directExtractionAgent = string.IsNullOrWhiteSpace(inferDeploymentName)
            ? null
            : client.GetChatClient(inferDeploymentName).AsAIAgent(instructions: DirectExtractionInstructions, name: "TocDirectExtractionAgent");
        _directExtractionRunOptions = string.IsNullOrWhiteSpace(inferDeploymentName) ? null : BuildDeterministicRunOptions(inferDeploymentName);

        // Same main deployment — splitting one module's own text into
        // engrams still needs genuine reading comprehension (telling apart
        // scaffolding from real content), not mechanical marker-finding.
        _engramSplitAgent = string.IsNullOrWhiteSpace(inferDeploymentName)
            ? null
            : client.GetChatClient(inferDeploymentName).AsAIAgent(instructions: EngramSplitInstructions, name: "TocEngramSplitAgent");
        _engramSplitRunOptions = string.IsNullOrWhiteSpace(inferDeploymentName) ? null : BuildDeterministicRunOptions(inferDeploymentName);
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

    /// <summary>Whether <see cref="ExtractDirectAsync"/> can be called.</summary>
    public bool IsDirectExtractionConfigured => _directExtractionAgent is not null;

    /// <summary>Whether <see cref="SplitModuleIntoEngramsAsync"/> can be called.</summary>
    public bool IsEngramSplitConfigured => _engramSplitAgent is not null;

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

    /// <summary>Result wrapper for <see cref="SplitModuleIntoEngramsAsync"/>
    /// — structured-output schemas need an object, not a bare array.</summary>
    public sealed class ModuleEngramSplit
    {
        public List<InferredOutlineNode> Engrams { get; set; } = [];
    }

    /// <summary>
    /// 2026-11-10: splits ONE already-identified module/chapter's own text
    /// (see <see cref="EngramSplitInstructions"/>) into real engram-level
    /// nodes — used by <see cref="HumanOS.Services.CapabilityOutlinePreviewService"/>
    /// Tier 0 when <see cref="HumanOS.Agents.Studio.SyllabusOutlineParser"/>
    /// found that module's own heading but no numbered/keyworded
    /// subsections under it, so it would otherwise become a single
    /// oversized node covering the module's ENTIRE text. Unlike
    /// <see cref="ExtractAsync"/>/<see cref="InferOutlineAsync"/> (whole
    /// document), this is scoped to just one module's own text — small
    /// enough to always fit in one call regardless of overall document
    /// size.
    /// </summary>
    public async Task<List<InferredOutlineNode>> SplitModuleIntoEngramsAsync(string moduleText, CancellationToken cancellationToken = default)
    {
        if (_engramSplitAgent is null)
        {
            throw new InvalidOperationException(
                "The TocExtraction engram-split agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var prompt = "Split this module's text into engrams:\n\n" + moduleText;
        var response = await _engramSplitAgent.RunAsync<ModuleEngramSplit>(prompt, options: _engramSplitRunOptions, cancellationToken: cancellationToken);
        return response.Result.Engrams;
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

    /// <summary>
    /// Single-call Chapter -&gt; Engram extraction WITH each engram's own
    /// verbatim content already attached (see <see cref="DirectExtractionInstructions"/>
    /// and <see cref="DirectExtraction"/>) — only used for documents small
    /// enough to fit in one context window (caller's own size threshold;
    /// see <see cref="HumanOS.Services.CapabilityOutlinePreviewService"/>).
    /// Since the model is explicitly allowed to choose engram content
    /// freely (not re-located via a short marker), every returned engram is
    /// verified here to be a genuine, ordinal, verbatim substring of
    /// <paramref name="documentText"/> — an engram that fails this check
    /// (the model paraphrased/invented instead of copying) is DROPPED
    /// rather than silently accepted with wrong/hallucinated content, so a
    /// caller can detect and fall back to the slower tiers if too much was
    /// dropped.
    /// </summary>
    public async Task<DirectExtraction> ExtractDirectAsync(string documentText, CancellationToken cancellationToken = default)
    {
        if (_directExtractionAgent is null)
        {
            throw new InvalidOperationException(
                "The TocExtraction direct-extraction agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var prompt = "Split the following document into a Chapter -> Engram structure, with each engram's own verbatim content:\n\n" + documentText;
        var response = await _directExtractionAgent.RunAsync<DirectExtraction>(prompt, options: _directExtractionRunOptions, cancellationToken: cancellationToken);

        foreach (var chapter in response.Result.Chapters)
        {
            chapter.Engrams = chapter.Engrams
                .Where(engram => !string.IsNullOrWhiteSpace(engram.Content)
                    && documentText.Contains(engram.Content, StringComparison.Ordinal))
                .ToList();
        }
        response.Result.Chapters = response.Result.Chapters.Where(c => c.Engrams.Count > 0).ToList();

        return response.Result;
    }
}
