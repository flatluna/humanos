using HumanOS.Agents.Studio;
using HumanOS.Services.DocumentClassification;
using SharpToken;

namespace HumanOS.Services;

/// <summary>One proposed NODE (fine-grained topic) under a chapter/level in
/// a <see cref="CapabilityOutlinePreviewResult"/> — see
/// <see cref="CapabilityOutlinePreviewService"/>.</summary>
public sealed class CapabilityOutlineNodeDto
{
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Short, EXACT, verbatim substring of the source document where this
    /// node's own content begins — round-tripped back to the backend
    /// unchanged (even if the designer edits <see cref="Title"/>) so a
    /// subsequent real run can locate this node's text via ordinal search
    /// WITHOUT re-running detection, guaranteeing the generation loop
    /// respects exactly the chapter/node names the designer approved. Not
    /// meant to be shown in the UI. Empty when <see cref="Content"/> is
    /// already set (2026-10-29 direct-extraction tier — no re-location is
    /// needed in that case).
    /// </summary>
    public string StartMarker { get; set; } = string.Empty;

    /// <summary>One-sentence capability this node/engram builds in the
    /// learner — populated only by the 2026-10-29 direct-extraction tier
    /// (see <see cref="HumanOS.Agents.Studio.DirectEngram.Capability"/>).</summary>
    public string? Capability { get; set; }

    /// <summary>Already-sliced, verbatim engram content — populated ONLY
    /// by the 2026-10-29 direct-extraction tier (see
    /// <see cref="HumanOS.Agents.Studio.TocExtractionAgent.ExtractDirectAsync"/>).
    /// When set, downstream code (<see cref="HumanOS.Agents.Studio.DocumentChapterSplitter"/>)
    /// uses this text AS-IS instead of re-locating <see cref="StartMarker"/>
    /// via ordinal search — this tier never produces a StartMarker at all.</summary>
    public string? Content { get; set; }

    /// <summary>Size estimate (2026-08-17, cost/scope preview) — the
    /// verbatim character count of THIS node's own slice of the source
    /// text, from its StartMarker up to the next node/chapter's start (or
    /// document end for the last one). Computed once by
    /// CapabilityOutlinePreviewService right before returning — 0 if the
    /// marker couldn't be re-located (should not normally happen).</summary>
    public int TotalCharacters { get; set; }

    /// <summary>Real token count (SharpToken, cl100k_base) for the same
    /// slice as <see cref="TotalCharacters"/> — the actual input-token cost
    /// this engram will contribute to Step 3 generation, so the designer
    /// can gauge total cost/scope BEFORE approving (Paso 5), not only
    /// after Step 2 extraction already ran.</summary>
    public int TotalTokens { get; set; }

    /// <summary>True when this node's own text slice is too small to be
    /// real developed content (2026-08-17) — e.g. a bare outline/index
    /// entry like "Week 1: Alphabet Review, Letter Names, Letter Sounds"
    /// with no actual explanatory prose. The Step 3 Designer Agent is
    /// strictly source-faithful (never invents), so a node this sparse
    /// would otherwise produce weak/empty Hypothesis/Learn/Recall content
    /// — the wizard offers AI content expansion for nodes flagged here
    /// (see SectionContentExpansionAgent) instead of running the normal
    /// pipeline on an almost-empty source.</summary>
    public bool HasSparseContent { get; set; }

    /// <summary>Real images (base64 data URLs, e.g. "data:image/png;base64,...")
    /// embedded in the source document's page(s) covering this node. Populated
    /// either when the outline was seeded from an already-extracted
    /// ProcessDocument's <see cref="HumanOS.Contracts.ProcessDocuments.ProcessStep.StepImages"/>
    /// (HumanOS Enterprise "Save as Capability" flow, 2026-11-03), OR
    /// post-detection for a direct PDF upload (see
    /// <see cref="AzureFunctions.Api.PreviewCapabilityOutlineFunction.AttachRealImages"/>)
    /// from <see cref="HumanOS.Services.PdfContentExtractionService.ExtractionResult.PageImageDataUrls"/>
    /// — so either course-creation path can show the designer real
    /// thumbnails per section, not just the AI-written text description
    /// already folded into <see cref="Content"/>.</summary>
    public List<string>? ImageDataUrls { get; set; }
}

/// <summary>One proposed CHAPTER (becomes a graph "level") in a
/// <see cref="CapabilityOutlinePreviewResult"/>.</summary>
public sealed class CapabilityOutlineChapterDto
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Short (1-2 sentence) generic description — populated only
    /// by the 2026-10-29 direct-extraction tier.</summary>
    public string? Description { get; set; }

    /// <summary>See <see cref="CapabilityOutlineNodeDto.StartMarker"/> —
    /// same contract, for the chapter/level's own boundary. Empty when
    /// every node under this chapter already carries its own
    /// <see cref="CapabilityOutlineNodeDto.Content"/>.</summary>
    public string StartMarker { get; set; } = string.Empty;

    public List<CapabilityOutlineNodeDto> Nodes { get; set; } = [];

    /// <summary>Sum of every node's <see cref="CapabilityOutlineNodeDto.TotalCharacters"/> under this chapter.</summary>
    public int TotalCharacters { get; set; }

    /// <summary>Sum of every node's <see cref="CapabilityOutlineNodeDto.TotalTokens"/> under this chapter.</summary>
    public int TotalTokens { get; set; }
}

/// <summary>How the outline in a <see cref="CapabilityOutlinePreviewResult"/>
/// was obtained — shown to the designer so they know how much scrutiny to
/// apply before approving (2026-08-04 design: "Modo A" vs "Modo B").</summary>
public enum CapabilityOutlineSource
{
    /// <summary>The document already has its own explicit chapter/section
    /// structure and it was located (deterministic regex Tier 0, or an LLM
    /// locating real existing headings as Tier 1 fallback) — low risk of
    /// inventing content that isn't there.</summary>
    Detected,

    /// <summary>The document has NO explicit index of its own — the model
    /// read the whole document and PROPOSED a pedagogical breakdown from
    /// scratch. Higher cost, and the designer should review more carefully
    /// before approving.</summary>
    InferredByAI,

    /// <summary>(2026-10-29) The document is small enough to fit in a
    /// single context window — the model read the WHOLE document and
    /// returned the Chapter/Engram structure WITH each engram's own
    /// verbatim content already attached in one call, instead of just a
    /// short marker for code to re-locate afterwards. See
    /// <see cref="HumanOS.Agents.Studio.TocExtractionAgent.ExtractDirectAsync"/>.</summary>
    DirectGenerated,

    /// <summary>No usable structure could be detected or inferred at all
    /// (e.g. empty/near-empty document).</summary>
    None
}

/// <summary>Result of <see cref="CapabilityOutlinePreviewService.PreviewAsync"/>
/// — the FULL two-level (chapter/level -> node) outline for the designer's
/// approval sidebar (Paso 5), before the actual (expensive) generation
/// pipeline ever runs.</summary>
public sealed class CapabilityOutlinePreviewResult
{
    public CapabilityOutlineSource Source { get; set; } = CapabilityOutlineSource.None;

    public List<CapabilityOutlineChapterDto> Chapters { get; set; } = [];

    /// <summary>Set ONLY by <see cref="CapabilityOutlinePreviewService.PreviewFromIdeaAsync"/>
    /// (the "Texto/idea" entry point, 2026-08-07) — the full document
    /// IdeaToDocumentAgent expanded the idea into, which THIS outline's
    /// StartMarkers were located against. Must be sent back VERBATIM as
    /// StartCapabilityGraphFromDescriptionFunction's PreExpandedDocumentText
    /// when submitting an ApprovedOutline — the real run must slice the
    /// SAME text these markers point into, since re-expanding the idea
    /// would produce a different (non-deterministic) document and every
    /// marker lookup would fail. Null for the PDF/raw-text preview path,
    /// where the caller already holds the source content itself (the
    /// file, or the pasted text) and can resend that unchanged instead.</summary>
    public string? ExpandedDocumentText { get; set; }

    /// <summary>Set only when <see cref="Source"/> is None — a
    /// human-readable explanation of why no structure could be found.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Document Type Detection Engine (2026-08-17) — the engine's
    /// recommendation for this document's structural type, computed
    /// whenever the caller doesn't already pass an explicit DocumentType
    /// override to PreviewAsync/PreviewFromIdeaAsync. Advisory only —
    /// the wizard shows this and lets the designer accept/override it
    /// before re-requesting the preview with their chosen type.</summary>
    public string? DetectedDocumentType { get; set; }

    public double? DetectedDocumentTypeConfidence { get; set; }

    /// <summary>Document-wide sum of every chapter's <see cref="CapabilityOutlineChapterDto.TotalCharacters"/> —
    /// lets the wizard show "~N characters / ~M tokens total" before the
    /// designer approves and pays for real generation.</summary>
    public int TotalCharacters { get; set; }

    /// <summary>Document-wide sum of every chapter's <see cref="CapabilityOutlineChapterDto.TotalTokens"/>.</summary>
    public int TotalTokens { get; set; }
}

/// <summary>
/// Cheap, detection-only preview of a document's chapter/node structure —
/// reuses the exact same 3-tier detection used by
/// <see cref="HumanOS.Agents.Studio.DocumentChapterSplitter"/> for the real
/// run, but WITHOUT running Curador/GraphArchitect/illustrations — just
/// text extraction + structure detection, so the designer can review and
/// approve/edit the plan BEFORE any expensive generation happens (2026-08-04
/// "Paso 5: Capability Design" — see /memories/repo design notes).
/// </summary>
public static class CapabilityOutlinePreviewService
{
    /// <summary>Minimum number of chapters Tier 1 (LLM boundary detection)
    /// must find before it's considered a usable "Detected" result rather
    /// than falling through to Tier 2 (AI inference from scratch).</summary>
    private const int MinChaptersForDetected = 2;

    /// <summary>(2026-10-29) Max source document size for the single-call
    /// "Direct" tier (see <see cref="CapabilityOutlineSource.DirectGenerated"/>)
    /// — above this, the model would have to reproduce most of the
    /// document's own text verbatim in its OUTPUT too (not just read it),
    /// risking truncated/invalid JSON; falls back to the older detect-
    /// marker-then-slice tiers instead.</summary>
    private const int DirectGenerationMaxCharacters = 75_000;

    /// <summary>(2026-11-10) Above <see cref="DirectGenerationMaxCharacters"/>
    /// but under this size (~60-70 pages, assuming ~3,500 characters/page
    /// once embedded-image descriptions are folded into the text), the LLM
    /// is STILL capable of reading the whole document in one Tier 1 call
    /// (<see cref="TocExtractionAgent.ExtractAsync"/>) to decide chapter/
    /// engram BOUNDARIES itself — so Tier 0's purely deterministic regex
    /// splitter (<see cref="SyllabusOutlineParser"/>) is skipped entirely
    /// in that range and Tier 1 runs instead, even when Tier 0 would
    /// otherwise report a confident structure. Per-course decision: the
    /// deterministic splitter never reliably tells real teachable content
    /// apart from scaffolding/assessment sections (it already produced the
    /// "Knowledge Check"/"Applied Challenge" standalone-engram bug fixed
    /// alongside this change), so the LLM should always be the one
    /// deciding engram boundaries up to this size; only beyond it (a
    /// genuinely huge document) does the regex tier remain as a fallback,
    /// for cost/latency reasons.</summary>
    private const int MaxCharactersForLlmOnlyDetection = 260_000;

    /// <summary>Below this token count, a node's own text slice is treated
    /// as a bare outline/index entry rather than real developed content
    /// (2026-08-17) — see CapabilityOutlineNodeDto.HasSparseContent's own
    /// doc comment. Chosen low enough that a genuinely short-but-real
    /// paragraph (e.g. a one-sentence definition) isn't flagged, but a
    /// bullet list of topic names with no prose is.</summary>
    private const int SparseContentTokenThreshold = 40;

    private static readonly GptEncoding TokenEncoding = GptEncoding.GetEncoding("cl100k_base");

    /// <summary>Size estimate pass (2026-08-17, cost/scope preview) — for
    /// EVERY node across every chapter (regardless of which detection tier
    /// produced them), re-locates its StartMarker in documentText (same
    /// ordinal-search contract DocumentChapterSplitter uses for an
    /// approved outline) and slices from there up to the NEXT node/chapter
    /// boundary (or document end for the very last one), then stamps
    /// TotalCharacters/TotalTokens on the node, its owning chapter (summed),
    /// and returns the document-wide totals. A node whose marker can't be
    /// re-located (should not normally happen) is simply left at 0/0.</summary>
    private static (int TotalCharacters, int TotalTokens) AnnotateSizes(string documentText, List<CapabilityOutlineChapterDto> chapters)
    {
        // Flatten every node (with a back-reference to its chapter) in
        // document order, alongside each chapter's own boundary — a
        // chapter's "size" is everything from its own start up to the
        // first of its nodes (usually ~0, since chapters/nodes normally
        // start at nearly the same offset), so only NODE spans matter for
        // the size math; chapter totals are just the sum of their nodes.
        var flattened = new List<(CapabilityOutlineNodeDto Node, int Offset)>();
        var searchFrom = 0;
        foreach (var chapter in chapters)
        {
            foreach (var node in chapter.Nodes)
            {
                if (string.IsNullOrEmpty(node.StartMarker))
                {
                    continue;
                }

                var offset = documentText.IndexOf(node.StartMarker, searchFrom, StringComparison.Ordinal);
                if (offset < 0)
                {
                    offset = documentText.IndexOf(node.StartMarker, StringComparison.Ordinal);
                }
                if (offset < 0)
                {
                    continue;
                }

                flattened.Add((node, offset));
                searchFrom = offset + 1;
            }
        }

        flattened.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        for (var i = 0; i < flattened.Count; i++)
        {
            var (node, offset) = flattened[i];
            var end = i + 1 < flattened.Count ? flattened[i + 1].Offset : documentText.Length;
            var length = Math.Max(0, end - offset);
            var slice = documentText.Substring(offset, length);
            node.TotalCharacters = slice.Length;
            node.TotalTokens = TokenEncoding.Encode(slice).Count;
            node.HasSparseContent = node.TotalTokens < SparseContentTokenThreshold;

            // Surface the node's own verbatim text slice for the preview UI
            // (2026-11-10 "Module 0 shows an empty source-text box instead
            // of its real text" fix) — the Direct-extraction tier already
            // sets Content itself, but Tier 0/0.5/1/2 (the regex/marker-
            // based detections that reach this method) previously left
            // Content null, so the designer's "Ver texto fuente" review box
            // rendered empty next to the page's real image, looking as if
            // the text itself had been replaced by an image.
            if (string.IsNullOrEmpty(node.Content))
            {
                node.Content = slice;
            }
        }

        var documentCharacters = 0;
        var documentTokens = 0;
        foreach (var chapter in chapters)
        {
            chapter.TotalCharacters = chapter.Nodes.Sum(n => n.TotalCharacters);
            chapter.TotalTokens = chapter.Nodes.Sum(n => n.TotalTokens);
            documentCharacters += chapter.TotalCharacters;
            documentTokens += chapter.TotalTokens;
        }

        return (documentCharacters, documentTokens);
    }

    /// <summary>Stateless/pure classifiers — safe as a static, shared
    /// instance (2026-08-17, Paso 3a/3b). Used to stamp DetectedDocumentType/
    /// Confidence on every preview result for the wizard to show, and (via
    /// DocumentTypeOutlineParser) to pick a type-aware splitting strategy
    /// when the caller passes an explicit/confirmed DocumentType.</summary>
    private static readonly DocumentClassificationEngine ClassificationEngine = new(
    [
        new HumanOS.Services.DocumentClassification.Classifiers.ContractClassifier(),
        new HumanOS.Services.DocumentClassification.Classifiers.LawClassifier(),
        new HumanOS.Services.DocumentClassification.Classifiers.PolicyClassifier(),
        new HumanOS.Services.DocumentClassification.Classifiers.ProcedureClassifier(),
        new HumanOS.Services.DocumentClassification.Classifiers.AcademicWithIndexClassifier(),
        new HumanOS.Services.DocumentClassification.Classifiers.AcademicNarrativeClassifier(),
        new HumanOS.Services.DocumentClassification.Classifiers.ResearchPaperClassifier()
    ]);

    public static async Task<CapabilityOutlinePreviewResult> PreviewAsync(
        string documentText,
        TocExtractionAgent tocExtraction,
        CancellationToken cancellationToken = default,
        DocumentType? documentType = null)
    {
        // Document Type Detection (2026-08-17, Paso 3a/3b) — if the caller
        // already knows/confirmed a type, use it as-is (never re-classify
        // and silently override the designer's own choice); otherwise
        // classify now so the wizard can show a recommendation on this
        // same preview call, at no extra round trip.
        var classification = documentType is null ? ClassificationEngine.Classify(documentText) : null;
        var effectiveType = documentType ?? classification!.PrimaryType;

        // ---- Tier "Direct" (2026-10-29): small enough to fit one context
        // window — skip ALL deterministic/regex-based detection below and
        // have the model read the whole document itself, directly handing
        // back chapters/engrams WITH their own verbatim content already
        // attached. This tier now runs FIRST (ahead of Tier 0.5/Tier 0)
        // because those regex tiers are a brittle false-positive source —
        // e.g. a numbered "Step 1:/Step 2:..." list in the body matches
        // SyllabusOutlineParser's module-heading heuristic and returns an
        // instant but low-fidelity structure, silently pre-empting the
        // higher-quality LLM pass this tier exists to provide. Only
        // attempted for documents under DirectGenerationMaxCharacters (the
        // model must reproduce ~all source text in its OUTPUT too); falls
        // through to the regex tiers and older Tier 1/Tier 2
        // (detect-marker-then-slice) tiers below if this returns nothing
        // usable (e.g. every engram failed the verbatim check) or isn't
        // configured.
        if (documentText.Length <= DirectGenerationMaxCharacters && tocExtraction.IsDirectExtractionConfigured)
        {
            var direct = await tocExtraction.ExtractDirectAsync(documentText, cancellationToken);
            var directChapters = direct.Chapters
                .Where(c => c.Engrams.Count > 0)
                .Select(c => new CapabilityOutlineChapterDto
                {
                    Title = c.Title,
                    Description = c.Description,
                    Nodes = c.Engrams.ConvertAll(e => new CapabilityOutlineNodeDto
                    {
                        Title = e.Title,
                        Capability = e.Capability,
                        Content = e.Content,
                        TotalCharacters = e.Content.Length,
                        TotalTokens = TokenEncoding.Encode(e.Content).Count
                    })
                })
                .ToList();

            if (directChapters.Count > 0)
            {
                var directTotalCharacters = directChapters.Sum(c => c.Nodes.Sum(n => n.TotalCharacters));
                var directTotalTokens = directChapters.Sum(c => c.Nodes.Sum(n => n.TotalTokens));
                foreach (var chapter in directChapters)
                {
                    chapter.TotalCharacters = chapter.Nodes.Sum(n => n.TotalCharacters);
                    chapter.TotalTokens = chapter.Nodes.Sum(n => n.TotalTokens);
                }

                return new CapabilityOutlinePreviewResult
                {
                    Source = CapabilityOutlineSource.DirectGenerated,
                    Chapters = directChapters,
                    DetectedDocumentType = effectiveType.ToString(),
                    DetectedDocumentTypeConfidence = classification?.Confidence,
                    TotalCharacters = directTotalCharacters,
                    TotalTokens = directTotalTokens
                };
            }
        }

        // ---- Tier 0.5: DocumentType-aware marker detection (no LLM) ----
        // Only Law/Contract/Procedure/ResearchPaper have a reliable enough
        // marker to trust here — see DocumentTypeOutlineParser's own doc
        // comment for why Policy/AcademicWithIndex/AcademicNarrative/Unknown
        // fall through to the existing tiers unchanged.
        var typeOutline = DocumentTypeOutlineParser.Parse(documentText, effectiveType);
        if (typeOutline.IsConfident)
        {
            var typeChapters = typeOutline.Chapters.ConvertAll(chapter => new CapabilityOutlineChapterDto
            {
                Title = chapter.Title,
                StartMarker = MarkerAt(documentText, chapter.StartOffset),
                Nodes = chapter.Nodes.ConvertAll(node => new CapabilityOutlineNodeDto
                {
                    Title = node.Title,
                    StartMarker = MarkerAt(documentText, node.StartOffset)
                })
            });
            var sizes = AnnotateSizes(documentText, typeChapters);
            return new CapabilityOutlinePreviewResult
            {
                Source = CapabilityOutlineSource.Detected,
                Chapters = typeChapters,
                DetectedDocumentType = effectiveType.ToString(),
                DetectedDocumentTypeConfidence = classification?.Confidence,
                TotalCharacters = sizes.TotalCharacters,
                TotalTokens = sizes.TotalTokens
            };
        }

        // ---- Tier 0: deterministic regex-based detection (no LLM) ----
        // Skipped (even when confident) for any document within the LLM's
        // own capacity (see MaxCharactersForLlmOnlyDetection) whenever an
        // LLM tier is actually available — the regex splitter only ever
        // looks at heading NUMBERS/keywords, so it can't tell a real
        // teachable subsection from a numbered review-question block like
        // "Knowledge Check 26. ..."; only past that size (or with no LLM
        // configured at all) does it remain as the fallback.
        var tier0 = SyllabusOutlineParser.Parse(documentText);
        var allowDeterministicTier0 = documentText.Length > MaxCharactersForLlmOnlyDetection || !tocExtraction.IsConfigured;
        if (tier0.IsConfidentStructure && allowDeterministicTier0)
        {
            var chapters = new List<CapabilityOutlineChapterDto>();
            foreach (var module in tier0.Modules)
            {
                List<CapabilityOutlineNodeDto> nodes;
                if (module.Subsections.Count > 0)
                {
                    // A regex-numbered subsection is still just a raw
                    // heading match — nothing here stops a "Knowledge
                    // Check 26." or "Applied Challenge" subsection
                    // (numbered because of ITS OWN review-question
                    // numbering, not real module structure) from being
                    // treated as a distinct engram. Apply the same
                    // assessment-title guardrail used on the LLM-split
                    // path (2026-11-10) before building nodes.
                    var located = module.Subsections.ConvertAll(subsection =>
                        (Title: BuildSubsectionTitle(subsection), Offset: subsection.StartOffset - module.StartOffset));
                    located = MergeAssessmentEngrams(located);
                    nodes = located.ConvertAll(e => new CapabilityOutlineNodeDto
                    {
                        Title = e.Title,
                        StartMarker = MarkerAt(documentText, module.StartOffset + e.Offset)
                    });
                }
                else
                {
                    // No numbered/keyworded subsections for this module
                    // (the common case for a prose module whose own
                    // internal headings are just plain named topics, e.g.
                    // "The Problem That Forced Computing Forward") — try
                    // an LLM pass over JUST this module's own text to split
                    // it into real engram-level sub-topics instead of
                    // lumping the whole module (including throwaway
                    // "Learning Objectives"/"Opening Conversation"
                    // scaffolding) into one oversized single node (2026-11-10).
                    nodes = await SplitModuleIntoEngramNodesAsync(documentText, module, tier0.Modules, tocExtraction, cancellationToken)
                        ?? [new CapabilityOutlineNodeDto { Title = BuildModuleTitle(module), StartMarker = MarkerAt(documentText, module.StartOffset) }];
                }

                chapters.Add(new CapabilityOutlineChapterDto
                {
                    Title = BuildModuleTitle(module),
                    StartMarker = MarkerAt(documentText, module.StartOffset),
                    Nodes = nodes
                });
            }

            var tier0Sizes = AnnotateSizes(documentText, chapters);

            return new CapabilityOutlinePreviewResult
            {
                Source = CapabilityOutlineSource.Detected,
                Chapters = chapters,
                DetectedDocumentType = effectiveType.ToString(),
                DetectedDocumentTypeConfidence = classification?.Confidence,
                TotalCharacters = tier0Sizes.TotalCharacters,
                TotalTokens = tier0Sizes.TotalTokens
            };
        }

        if (!tocExtraction.IsConfigured)
        {
            return new CapabilityOutlinePreviewResult
            {
                Source = CapabilityOutlineSource.None,
                RejectionReason = tier0.RejectionReason ?? "El documento no presenta una estructura de capítulos detectable.",
                DetectedDocumentType = effectiveType.ToString(),
                DetectedDocumentTypeConfidence = classification?.Confidence
            };
        }

        // ---- Tier 1: LLM boundary detection over the document's own headings ----
        var toc = await tocExtraction.ExtractAsync(documentText, strictSyllabusMode: true, cancellationToken);
        var locatedChapters = toc.Chapters
            .Where(c => !string.IsNullOrWhiteSpace(c.StartMarker) && documentText.Contains(c.StartMarker, StringComparison.Ordinal))
            .ToList();

        // A document that contains BOTH its own Table of Contents listing
        // AND the real body headings can make the model return the SAME
        // chapter/subsection twice (once found in each place) — collapse
        // duplicates before grouping, keeping whichever occurrence sits
        // LATER in the document (the real body heading; a TOC listing
        // always precedes the content it summarizes).
        locatedChapters = DeduplicateByTitle(locatedChapters, documentText);

        if (locatedChapters.Count >= MinChaptersForDetected)
        {
            var grouped = GroupTier1Chapters(locatedChapters);
            if (grouped.Count > 0)
            {
                var tier1Sizes = AnnotateSizes(documentText, grouped);
                return new CapabilityOutlinePreviewResult
                {
                    Source = CapabilityOutlineSource.Detected,
                    Chapters = grouped,
                    DetectedDocumentType = effectiveType.ToString(),
                    DetectedDocumentTypeConfidence = classification?.Confidence,
                    TotalCharacters = tier1Sizes.TotalCharacters,
                    TotalTokens = tier1Sizes.TotalTokens
                };
            }
        }

        // ---- Tier 2: no real index at all — propose one from scratch ----
        if (tocExtraction.IsInferenceConfigured)
        {
            var inferred = await tocExtraction.InferOutlineAsync(documentText, cancellationToken);
            var chapters = inferred.Chapters
                .Where(c => c.Nodes.Count > 0)
                .Select(c => new CapabilityOutlineChapterDto
                {
                    Title = c.Title,
                    StartMarker = c.StartMarker,
                    Nodes = c.Nodes.ConvertAll(n => new CapabilityOutlineNodeDto { Title = n.Title, StartMarker = n.StartMarker })
                })
                .ToList();

            if (chapters.Count > 0)
            {
                var tier2Sizes = AnnotateSizes(documentText, chapters);
                return new CapabilityOutlinePreviewResult
                {
                    Source = CapabilityOutlineSource.InferredByAI,
                    Chapters = chapters,
                    DetectedDocumentType = effectiveType.ToString(),
                    DetectedDocumentTypeConfidence = classification?.Confidence,
                    TotalCharacters = tier2Sizes.TotalCharacters,
                    TotalTokens = tier2Sizes.TotalTokens
                };
            }
        }

        return new CapabilityOutlinePreviewResult
        {
            Source = CapabilityOutlineSource.None,
            RejectionReason = tier0.RejectionReason ?? "No se pudo detectar ni inferir una estructura de capítulos para este documento.",
            DetectedDocumentType = effectiveType.ToString(),
            DetectedDocumentTypeConfidence = classification?.Confidence
        };
    }

    /// <summary>
    /// "Texto/idea" entry point (2026-08-07): unlike <see cref="PreviewAsync"/>,
    /// there is no document yet at all \u2014 only a short user-written idea.
    /// Expands it via <see cref="IdeaToDocumentAgent"/> FIRST (the exact
    /// same expansion the real run would otherwise do at generation time),
    /// then runs the normal chapter/node detection against THAT generated
    /// document \u2014 never against the raw one-sentence idea, whose
    /// StartMarkers could never be relocated in the much longer document a
    /// later, separate expansion call would produce. The returned
    /// <see cref="CapabilityOutlinePreviewResult.ExpandedDocumentText"/>
    /// must be sent back verbatim (as PreExpandedDocumentText) when the
    /// approved outline is submitted, so the real run reuses THIS exact
    /// text instead of calling IdeaToDocumentAgent a second time.
    /// </summary>
    public static async Task<CapabilityOutlinePreviewResult> PreviewFromIdeaAsync(
        string capabilityName,
        string description,
        string languageCode,
        bool strictSyllabusMode,
        IdeaToDocumentAgent ideaToDocument,
        TocExtractionAgent tocExtraction,
        CancellationToken cancellationToken = default,
        DocumentType? documentType = null)
    {
        if (!ideaToDocument.IsConfigured)
        {
            return new CapabilityOutlinePreviewResult
            {
                Source = CapabilityOutlineSource.None,
                RejectionReason = "El agente IdeaToDocument no está configurado."
            };
        }

        var expansion = await ideaToDocument.ExpandAsync(capabilityName, description, languageCode, strictSyllabusMode, cancellationToken);

        var result = await PreviewAsync(expansion.DocumentText, tocExtraction, cancellationToken, documentType);
        result.ExpandedDocumentText = expansion.DocumentText;
        return result;
    }

    /// <summary>
    /// A document that contains BOTH its own Table of Contents listing AND
    /// the real body headings can make the model return the SAME chapter
    /// or subsection twice (once found in each place). Collapses entries
    /// whose (Title, ParentModuleTitle) match after normalizing whitespace
    /// case-insensitively, keeping whichever occurrence sits LATER in the
    /// document — a TOC listing always precedes the content it
    /// summarizes, so the later match is the real section heading.
    /// </summary>
    private static List<TocChapter> DeduplicateByTitle(List<TocChapter> chapters, string documentText)
    {
        var order = new List<(string Title, string? Parent)>();
        var bestByKey = new Dictionary<(string Title, string? Parent), (TocChapter Chapter, int Offset)>();

        foreach (var chapter in chapters)
        {
            var key = (Normalize(chapter.Title), chapter.ParentModuleTitle is null ? null : Normalize(chapter.ParentModuleTitle));
            var offset = documentText.IndexOf(chapter.StartMarker, StringComparison.Ordinal);

            if (!bestByKey.TryGetValue(key, out var existing))
            {
                order.Add(key);
                bestByKey[key] = (chapter, offset);
            }
            else if (offset > existing.Offset)
            {
                bestByKey[key] = (chapter, offset);
            }
        }

        return order.Select(key => bestByKey[key].Chapter).ToList();
    }

    private static string Normalize(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim().ToLowerInvariant();

    /// <summary>
    /// Groups Tier 1's flat chapter list back into a 2-level shape using
    /// each chapter's <see cref="TocChapter.ParentModuleTitle"/> (set only
    /// in strict mode — see <see cref="TocExtractionAgent"/>), preserving
    /// the order each parent group first appears in. A chapter with no
    /// ParentModuleTitle becomes its own single-node chapter (mirrors
    /// Tier 0's "module with no subsections" case).
    /// </summary>
    private static List<CapabilityOutlineChapterDto> GroupTier1Chapters(List<TocChapter> chapters)
    {
        // Two passes: a chapter can appear BOTH as its own standalone entry
        // (no ParentModuleTitle — the heading line itself) AND as the
        // ParentModuleTitle referenced by its own subsections' entries.
        // Register by title first so both references resolve to the SAME
        // dto instead of the standalone entry and the grouped-by-parent
        // entry becoming two separate duplicate chapters with an identical
        // title (this was the "1.0 Introduction" appearing twice bug).
        var order = new List<string>();
        var chapterByTitle = new Dictionary<string, CapabilityOutlineChapterDto>();

        CapabilityOutlineChapterDto GetOrCreate(string title, string startMarker)
        {
            if (!chapterByTitle.TryGetValue(title, out var dto))
            {
                dto = new CapabilityOutlineChapterDto { Title = title, StartMarker = startMarker };
                chapterByTitle[title] = dto;
                order.Add(title);
            }
            return dto;
        }

        foreach (var chapter in chapters)
        {
            var parentTitle = chapter.ParentModuleTitle;
            if (string.IsNullOrWhiteSpace(parentTitle))
            {
                // The chapter heading itself — don't add a self-node yet;
                // only done as a fallback below if it never gets any real
                // subsections attached.
                GetOrCreate(chapter.Title, chapter.StartMarker);
                continue;
            }

            var parentChapter = GetOrCreate(parentTitle, chapter.StartMarker);
            parentChapter.Nodes.Add(new CapabilityOutlineNodeDto { Title = chapter.Title, StartMarker = chapter.StartMarker });
        }

        var result = new List<CapabilityOutlineChapterDto>(order.Count);
        foreach (var title in order)
        {
            var dto = chapterByTitle[title];
            if (dto.Nodes.Count == 0)
            {
                dto.Nodes.Add(new CapabilityOutlineNodeDto { Title = dto.Title, StartMarker = dto.StartMarker });
            }
            else
            {
                // Deterministic backstop (2026-11-10) — the LLM is
                // instructed not to give an assessment/practice/review
                // heading its own subsection (see
                // StrictSyllabusModeAddendum), but is not perfectly
                // reliable; drop any that slipped through so it folds
                // into the PRECEDING real subsection's span instead (see
                // MergeAssessmentEngrams, same keyword list).
                dto.Nodes = MergeAssessmentEngramNodes(dto.Nodes);
            }
            result.Add(dto);
        }

        return result;
    }

    private static string BuildModuleTitle(SyllabusModule module)
    {
        // Bare-numeric convention (no keyword, e.g. "1.0 Introduction")
        // already carries its own full display title verbatim.
        if (string.IsNullOrEmpty(module.Keyword))
        {
            return module.Title;
        }

        var title = string.IsNullOrWhiteSpace(module.Title) ? $"{module.Keyword} {module.Number}" : module.Title;
        return $"{module.Keyword} {module.Number}: {title}";
    }

    private static string BuildSubsectionTitle(SyllabusSubsection subsection) =>
        string.IsNullOrWhiteSpace(subsection.Title) ? subsection.Number : $"{subsection.Number} {subsection.Title}";

    /// <summary>Short, verbatim marker starting exactly at <paramref name="offset"/>
    /// — reused as this item's StartMarker so downstream re-location (via
    /// ordinal search) works identically regardless of which detection tier
    /// produced it.</summary>
    private static string MarkerAt(string documentText, int offset)
    {
        if (offset < 0 || offset >= documentText.Length)
        {
            return string.Empty;
        }

        const int MarkerLength = 60;
        var length = Math.Min(MarkerLength, documentText.Length - offset);
        return documentText.Substring(offset, length).Trim();
    }

    /// <summary>Below this character count, a module with no detected
    /// subsections is treated as already a single coherent idea — not
    /// worth an extra LLM call to (at best) confirm it's one engram.</summary>
    private const int MinModuleCharactersForEngramSplit = 2000;

    /// <summary>A proposed engram span shorter than this many characters is
    /// scaffolding/framing text, not a real teachable idea on its own (see
    /// <see cref="MergeSparseEngramSpans"/>) — matches the spirit of
    /// <see cref="SparseContentTokenThreshold"/> (40 tokens, ~160
    /// characters) but a bit more generous since a short-but-real
    /// single-paragraph engram should still survive.</summary>
    private const int MinEngramSpanCharacters = 500;

    /// <summary>Titles that identify an ASSESSMENT/PRACTICE/REVIEW section
    /// — content whose job is to VERIFY or REINFORCE knowledge the learner
    /// is assumed to already have from an earlier engram, never to teach
    /// something new — and so must NEVER survive as its own standalone
    /// engram node, no matter how long its own span is (a long list of
    /// quiz questions is still not new content). <see cref="EngramTitleLooksLikeAssessment"/>
    /// is a deterministic, title-based guardrail layered on top of the
    /// LLM's own instruction to the same effect (see
    /// <see cref="TocExtractionAgent"/>'s EngramSplitInstructions) and on
    /// top of <see cref="MergeSparseEngramSpans"/>'s span-length check —
    /// a short "Knowledge Check" heading followed by several review
    /// questions can easily exceed <see cref="MinEngramSpanCharacters"/>
    /// and would otherwise slip through as its own (confusing) engram
    /// (2026-11-10).</summary>
    private static readonly string[] AssessmentEngramTitleKeywords =
    [
        "knowledge check",
        "in-class learning activity",
        "in class learning activity",
        "applied challenge",
        "profe recap",
        "recap",
        "oral defense question",
        "practice activity",
        "review question",
        "quiz",
        "self-check",
        "self check",
        "pause and explain",
        "diagnostic activity"
    ];

    private static bool EngramTitleLooksLikeAssessment(string title) =>
        AssessmentEngramTitleKeywords.Any(keyword => title.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    /// <summary>Tier 1 (<see cref="TocExtractionAgent.ExtractAsync"/>)
    /// counterpart to <see cref="MergeAssessmentEngrams"/> — operates on
    /// already-built <see cref="CapabilityOutlineNodeDto"/> instances
    /// (Tier 1 has no local offsets to work with; order in the list IS
    /// document order already) instead of raw (Title, Offset) tuples.
    /// Same rule: drop any node whose title matches
    /// <see cref="AssessmentEngramTitleKeywords"/> so its span is absorbed
    /// by the PRECEDING node — except the very first node, which is never
    /// dropped (the second is removed instead if that one is the
    /// assessment section) — and never remove the chapter's only
    /// node.</summary>
    private static List<CapabilityOutlineNodeDto> MergeAssessmentEngramNodes(List<CapabilityOutlineNodeDto> nodes)
    {
        var result = new List<CapabilityOutlineNodeDto>(nodes);
        for (var i = result.Count - 1; i >= 0; i--)
        {
            if (result.Count <= 1 || !EngramTitleLooksLikeAssessment(result[i].Title))
            {
                continue;
            }

            result.RemoveAt(i == 0 ? 1 : i);
        }

        return result;
    }

    /// <summary>Tier 0 (<see cref="SyllabusOutlineParser"/>) only detects
    /// NUMBERED/keyworded subsections — a module whose own internal
    /// headings are just plain named topics (e.g. "The Problem That Forced
    /// Computing Forward") gets zero subsections there and previously fell
    /// back to ONE node covering the whole module, lumping every distinct
    /// idea inside it (including throwaway "Learning Objectives"/"Opening
    /// Conversation" scaffolding) into one oversized engram (2026-11-10).
    /// This runs a cheap, module-scoped LLM pass (see
    /// <see cref="TocExtractionAgent.SplitModuleIntoEngramsAsync"/>) to
    /// propose real engram-level nodes for just THIS module's own text
    /// instead — returns null (caller falls back to the single-node
    /// result) when the agent isn't configured, the module is too short to
    /// bother, or the model's proposed engrams can't be re-located
    /// verbatim in the source.</summary>
    private static async Task<List<CapabilityOutlineNodeDto>?> SplitModuleIntoEngramNodesAsync(
        string documentText,
        SyllabusModule module,
        List<SyllabusModule> allModules,
        TocExtractionAgent tocExtraction,
        CancellationToken cancellationToken)
    {
        if (!tocExtraction.IsEngramSplitConfigured)
        {
            return null;
        }

        var moduleIndex = allModules.IndexOf(module);
        var moduleEnd = moduleIndex >= 0 && moduleIndex + 1 < allModules.Count
            ? allModules[moduleIndex + 1].StartOffset
            : documentText.Length;
        var moduleStart = module.StartOffset;
        if (moduleEnd <= moduleStart || moduleEnd - moduleStart < MinModuleCharactersForEngramSplit)
        {
            return null;
        }

        var moduleText = documentText.Substring(moduleStart, moduleEnd - moduleStart);

        List<InferredOutlineNode> engrams;
        try
        {
            engrams = await tocExtraction.SplitModuleIntoEngramsAsync(moduleText, cancellationToken);
        }
        catch
        {
            // Best-effort — a transient model/API failure here must never
            // break the whole outline preview; fall back to one node.
            return null;
        }

        if (engrams.Count < 2)
        {
            return null;
        }

        var located = new List<(string Title, int Offset)>();
        var searchFrom = 0;
        foreach (var engram in engrams)
        {
            if (string.IsNullOrWhiteSpace(engram.Title) || string.IsNullOrWhiteSpace(engram.StartMarker))
            {
                continue;
            }

            var offsetInModule = moduleText.IndexOf(engram.StartMarker, searchFrom, StringComparison.Ordinal);
            if (offsetInModule < 0)
            {
                offsetInModule = moduleText.IndexOf(engram.StartMarker, StringComparison.Ordinal);
            }
            if (offsetInModule < 0)
            {
                continue;
            }

            searchFrom = offsetInModule + 1;
            located.Add((engram.Title, offsetInModule));
        }

        located = MergeAssessmentEngrams(located);
        located = MergeSparseEngramSpans(located, moduleText.Length);

        if (located.Count < 2)
        {
            return null;
        }

        var nodes = located.ConvertAll(e => new CapabilityOutlineNodeDto
        {
            Title = e.Title,
            StartMarker = MarkerAt(documentText, moduleStart + e.Offset)
        });

        return nodes;
    }

    /// <summary>Deterministic, title-based companion to
    /// <see cref="MergeSparseEngramSpans"/> (2026-11-10) — an assessment/
    /// practice/review section (see <see cref="AssessmentEngramTitleKeywords"/>)
    /// must NEVER become its own engram regardless of how long its own
    /// span is (a "Knowledge Check" with several review questions can
    /// easily clear the span-length guardrail below). Folds every
    /// boundary whose title matches into the PRECEDING boundary (it
    /// checks/reinforces content already taught earlier, never content
    /// yet to come) — except the very first boundary, which can never be
    /// dropped, and instead absorbs the SECOND boundary into itself if
    /// that one is the assessment section.</summary>
    private static List<(string Title, int Offset)> MergeAssessmentEngrams(List<(string Title, int Offset)> located)
    {
        var result = new List<(string Title, int Offset)>(located);
        result.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        for (var i = result.Count - 1; i >= 0; i--)
        {
            if (result.Count <= 1 || !EngramTitleLooksLikeAssessment(result[i].Title))
            {
                continue;
            }

            result.RemoveAt(i == 0 ? 1 : i);
        }

        return result;
    }

    /// <summary>The model is only lightly instructed not to make a
    /// standalone engram out of pure scaffolding (e.g. "Learning
    /// Objectives", "Opening Conversation") — this is a deterministic
    /// code-level guardrail that drops any boundary whose own span (up to
    /// the NEXT remaining boundary, or the module's end for the last one)
    /// is below <see cref="MinEngramSpanCharacters"/>, folding its text
    /// into the adjacent engram instead (into the PREVIOUS one, except the
    /// very first boundary — which can never be dropped — folds the
    /// SECOND one into itself instead). Repeats until every remaining span
    /// is large enough or only one boundary is left.</summary>
    private static List<(string Title, int Offset)> MergeSparseEngramSpans(List<(string Title, int Offset)> located, int moduleTextLength)
    {
        var result = new List<(string Title, int Offset)>(located);
        result.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        while (result.Count > 1)
        {
            var mergedAny = false;
            for (var i = 0; i < result.Count; i++)
            {
                var end = i + 1 < result.Count ? result[i + 1].Offset : moduleTextLength;
                var span = end - result[i].Offset;
                if (span >= MinEngramSpanCharacters)
                {
                    continue;
                }

                // Drop whichever boundary keeps the first engram covering
                // the module's very start: the span at index 0 can never
                // be removed, so a too-short FIRST span instead absorbs
                // index 1 into it.
                result.RemoveAt(i == 0 ? 1 : i);
                mergedAny = true;
                break;
            }

            if (!mergedAny)
            {
                break;
            }
        }

        return result;
    }
}
