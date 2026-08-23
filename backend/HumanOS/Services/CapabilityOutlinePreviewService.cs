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
    /// meant to be shown in the UI.
    /// </summary>
    public string StartMarker { get; set; } = string.Empty;

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
}

/// <summary>One proposed CHAPTER (becomes a graph "level") in a
/// <see cref="CapabilityOutlinePreviewResult"/>.</summary>
public sealed class CapabilityOutlineChapterDto
{
    public string Title { get; set; } = string.Empty;

    /// <summary>See <see cref="CapabilityOutlineNodeDto.StartMarker"/> —
    /// same contract, for the chapter/level's own boundary.</summary>
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
        var tier0 = SyllabusOutlineParser.Parse(documentText);
        if (tier0.IsConfidentStructure)
        {
            var chapters = tier0.Modules.ConvertAll(module => new CapabilityOutlineChapterDto
            {
                Title = BuildModuleTitle(module),
                StartMarker = MarkerAt(documentText, module.StartOffset),
                Nodes = module.Subsections.Count > 0
                    ? module.Subsections.ConvertAll(subsection => new CapabilityOutlineNodeDto
                    {
                        Title = BuildSubsectionTitle(subsection),
                        StartMarker = MarkerAt(documentText, subsection.StartOffset)
                    })
                    : [new CapabilityOutlineNodeDto { Title = BuildModuleTitle(module), StartMarker = MarkerAt(documentText, module.StartOffset) }]
            });
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
}
