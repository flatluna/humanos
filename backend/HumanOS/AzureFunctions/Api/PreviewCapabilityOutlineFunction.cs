using System.Net;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Services;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// "Paso 5: Diseño de la Capability" (2026-08-04): a cheap, detection-only
/// preview of a document's chapter/node structure — runs ONLY text
/// extraction + the 3-tier chapter/subsection detection (see
/// <see cref="CapabilityOutlinePreviewService"/>), WITHOUT running the
/// expensive Curador/GraphArchitect/illustrations pipeline, so the designer
/// can review and approve (or hand-edit) the plan BEFORE any real
/// generation happens. The (possibly edited) result should be sent back
/// as-is in <c>ApprovedOutline</c> to <see cref="StartPdfCapabilityGraphFunction"/>/
/// <see cref="StartCapabilityGraphFromDescriptionFunction"/> so the real run
/// honors it verbatim instead of re-detecting.
/// </summary>
public sealed class PreviewCapabilityOutlineFunction
{
    private const int DefaultMaxImagesToDescribe = 60;
    private const int DefaultMaxImagesPerPage = 8;

    private readonly TocExtractionAgent _tocExtraction;
    private readonly PdfImageDescriptionAgent _pdfImageDescription;
    private readonly PdfContentExtractionService _pdfContentExtraction;
    private readonly IdeaToDocumentAgent _ideaToDocument;
    private readonly IConfiguration _configuration;

    public PreviewCapabilityOutlineFunction(
        TocExtractionAgent tocExtraction,
        PdfImageDescriptionAgent pdfImageDescription,
        PdfContentExtractionService pdfContentExtraction,
        IdeaToDocumentAgent ideaToDocument,
        IConfiguration configuration)
    {
        _tocExtraction = tocExtraction;
        _pdfImageDescription = pdfImageDescription;
        _pdfContentExtraction = pdfContentExtraction;
        _ideaToDocument = ideaToDocument;
        _configuration = configuration;
    }

    public sealed class PreviewRequest
    {
        /// <summary>Required when previewing an uploaded PDF — same
        /// base64-body convention as StartPdfCapabilityGraphFunction.
        /// Mutually exclusive with <see cref="Text"/>.</summary>
        public string? FileName { get; set; }

        public string? ContentBase64 { get; set; }

        /// <summary>Required when previewing the "Texto/idea" entry point
        /// instead of a PDF — the user's short idea/goal description, e.g.
        /// "Capacidad para que un niño aprenda a sumar y restar". Mutually
        /// exclusive with <see cref="ContentBase64"/>. Expanded into a full
        /// document via IdeaToDocumentAgent BEFORE chapter/node detection
        /// runs (2026-08-07) — see
        /// <see cref="CapabilityOutlinePreviewService.PreviewFromIdeaAsync"/>.
        /// Requires <see cref="CapabilityName"/> to also be set.</summary>
        public string? Text { get; set; }

        /// <summary>Required together with <see cref="Text"/> — same value
        /// the real run's CapabilityName will use, passed to
        /// IdeaToDocumentAgent for the expansion.</summary>
        public string? CapabilityName { get; set; }

        /// <summary>Alternate entry point (2026-10-07) for callers that already
        /// have a COMPLETE, final document (e.g. the Academic Course Generator's
        /// ProfesorExpert agent output) and must NOT have it rewritten/summarized
        /// by IdeaToDocumentAgent — unlike <see cref="Text"/>, this is used EXACTLY
        /// as given: only chapter/node detection runs against it, no expansion.
        /// Mutually exclusive with <see cref="Text"/> and <see cref="ContentBase64"/>.
        /// The response's ExpandedDocumentText simply echoes this value back, so the
        /// caller submits it unchanged as PreExpandedDocumentText afterwards, same as
        /// the idea/text flow.</summary>
        public string? AlreadyExpandedText { get; set; }

        /// <summary>Optional, only used together with <see cref="Text"/> —
        /// "es" or "en", defaults to "es". See
        /// StartCapabilityGraphFromDescriptionFunction's identical field.</summary>
        public string? LanguageCode { get; set; }

        /// <summary>Optional, only used together with <see cref="Text"/> —
        /// see StartCapabilityGraphFromDescriptionFunction's identical
        /// field. Passed through to the IdeaToDocumentAgent expansion so
        /// it mirrors an explicit numbered outline already present in the
        /// idea description, same as the real run would.</summary>
        public bool StrictSyllabusMode { get; set; }

        /// <summary>Optional (2026-08-17, Paso 3a/3b) — one of
        /// HumanOS.Services.DocumentClassification.DocumentType's names
        /// (e.g. "Law"/"Procedure"). When provided (the designer already
        /// confirmed a type in the wizard), skips re-classification and
        /// uses THIS type to pick the chapter/node splitting strategy.
        /// Omit/null to let the engine classify the document itself and
        /// return its recommendation in the response.</summary>
        public string? DocumentType { get; set; }
    }

    [Function("PreviewCapabilityOutline")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/capability-graph/preview-outline")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var body = await JsonSerializer.DeserializeAsync<PreviewRequest>(
            request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        var hasPdf = body is not null && !string.IsNullOrWhiteSpace(body.ContentBase64) && !string.IsNullOrWhiteSpace(body.FileName);
        var hasText = body is not null && !string.IsNullOrWhiteSpace(body.Text);
        var hasAlreadyExpandedText = body is not null && !string.IsNullOrWhiteSpace(body.AlreadyExpandedText);

        if (body is null || (!hasPdf && !hasText && !hasAlreadyExpandedText))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidRequest",
                "Either ('fileName' + 'contentBase64'), 'text', or 'alreadyExpandedText' is required.",
                cancellationToken);
        }

        if (hasText && string.IsNullOrWhiteSpace(body!.CapabilityName))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidRequest",
                "'capabilityName' is required together with 'text'.",
                cancellationToken);
        }

        HumanOS.Services.DocumentClassification.DocumentType? documentType = null;
        if (!string.IsNullOrWhiteSpace(body!.DocumentType))
        {
            if (!Enum.TryParse<HumanOS.Services.DocumentClassification.DocumentType>(body.DocumentType, out var parsedType))
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "InvalidDocumentType",
                    "documentType must be one of: " + string.Join(", ", Enum.GetNames<HumanOS.Services.DocumentClassification.DocumentType>()),
                    cancellationToken);
            }
            documentType = parsedType;
        }

        // "Already expanded" entry point (2026-10-07): the caller already has a
        // COMPLETE, final document (e.g. the Academic Course Generator's
        // ProfesorExpert output) — run ONLY chapter/node detection against it,
        // never IdeaToDocumentAgent, so the real authored content is never
        // rewritten/summarized.
        if (hasAlreadyExpandedText)
        {
            var alreadyExpandedResult = await CapabilityOutlinePreviewService.PreviewAsync(
                body!.AlreadyExpandedText!,
                _tocExtraction,
                cancellationToken,
                documentType);
            alreadyExpandedResult.ExpandedDocumentText = body.AlreadyExpandedText;

            return await FunctionResponseFactory.SuccessResponseAsync(request, alreadyExpandedResult, cancellationToken: cancellationToken);
        }

        // "Texto/idea" entry point (2026-08-07): expand the idea into a
        // real document FIRST, then run detection against that document —
        // never against the raw idea sentence. See
        // CapabilityOutlinePreviewService.PreviewFromIdeaAsync's doc
        // comment for why (StartMarker relocation across a later, separate
        // expansion call would otherwise never match).
        if (hasText)
        {
            var ideaResult = await CapabilityOutlinePreviewService.PreviewFromIdeaAsync(
                body!.CapabilityName!,
                body.Text!,
                string.IsNullOrWhiteSpace(body.LanguageCode) ? "es" : body.LanguageCode.Trim().ToLowerInvariant(),
                body.StrictSyllabusMode,
                _ideaToDocument,
                _tocExtraction,
                cancellationToken,
                documentType);

            return await FunctionResponseFactory.SuccessResponseAsync(request, ideaResult, cancellationToken: cancellationToken);
        }

        string documentText;
        byte[] pdfBytes;
        try
        {
            pdfBytes = Convert.FromBase64String(body.ContentBase64!);
        }
        catch (FormatException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBase64", "'contentBase64' is not valid base64.", cancellationToken);
        }

        // Extract text AND describe embedded images (2026-08-05) — the
        // SAME extraction the real run uses (see
        // PdfContentExtractionService's doc comment) — so a scanned/
        // image-heavy PDF's chapter/index detection here is based on
        // its actual content instead of PdfPig's plain text alone,
        // which is empty (or near-empty) for such documents.
        var maxImagesToDescribe = _configuration.GetValue<int?>("MaxCapabilityPdfImagesToDescribe") ?? DefaultMaxImagesToDescribe;
        var maxImagesPerPage = _configuration.GetValue<int?>("MaxCapabilityPdfImagesPerPage") ?? DefaultMaxImagesPerPage;

        var extraction = await _pdfContentExtraction.ExtractAsync(
            pdfBytes, _pdfImageDescription, maxImagesToDescribe, maxImagesPerPage, reportProgress: null, cancellationToken);

        documentText = extraction.CombinedText;

        if (string.IsNullOrWhiteSpace(documentText))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "EmptyDocument", "No extractable text was found.", cancellationToken);
        }

        var result = await CapabilityOutlinePreviewService.PreviewAsync(documentText, _tocExtraction, cancellationToken, documentType);

        AttachRealImages(result, documentText, extraction);

        return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
    }

    /// <summary>Locates each outline node's own text (its <see cref="CapabilityOutlineNodeDto.Content"/>
    /// when set by the direct-extraction tier, otherwise its <see cref="CapabilityOutlineNodeDto.StartMarker"/>)
    /// inside <paramref name="documentText"/>, figures out which original PDF
    /// page that offset falls on via <paramref name="extraction"/>'s
    /// <see cref="PdfContentExtractionService.ExtractionResult.PageOffsets"/>,
    /// and attaches that page's real images (if any) as
    /// <see cref="CapabilityOutlineNodeDto.ImageDataUrls"/> — so a
    /// direct-PDF-upload course can show real thumbnails per section, not
    /// just the AI-written text description already folded into the page's
    /// text. Best-effort: a node whose marker can't be re-located simply
    /// gets no images, same as its existing TotalCharacters/TotalTokens
    /// annotation already tolerates.</summary>
    private static void AttachRealImages(
        CapabilityOutlinePreviewResult result,
        string documentText,
        PdfContentExtractionService.ExtractionResult extraction)
    {
        if (extraction.PageImageDataUrls.Count == 0 || extraction.PageOffsets.Count == 0)
        {
            return;
        }

        // A single PDF page can be split into MANY chapters/nodes (e.g. an
        // image-only, image-heavy page inferred into several engrams by
        // Tier 2 — see InferOutlineAsync). Attaching that page's full image
        // list to every one of those nodes would show the exact same
        // image(s) repeated across every section in the UI. A page can
        // also hold SEVERAL distinct images that end up split across
        // DIFFERENT chapters/nodes (e.g. two images on the same single
        // page become two separate chapters) — in that case, handing the
        // whole page's image list to only the very first node would
        // wrongly give every image to one chapter and none to the rest.
        // Instead, each distinct image on the page is matched to the node
        // whose own text falls right after that image's own
        // "[Descripción de imagen — página N]" marker in the page's
        // text (in document order), and is then handed out ONCE — every
        // later node that maps to the SAME image gets none, avoiding
        // duplicate thumbnails while still letting different images on
        // one page reach their own separate chapters/nodes.
        var assignedImageIndexesByPage = new Dictionary<int, HashSet<int>>();
        var imageMarkerOffsetsByPage = new Dictionary<int, List<int>>();

        // Flattened, offset-sorted (node, offset, page) list across every
        // chapter — built ONCE up front (2026-11-10) so every pass below
        // (title-matching, positional, continuation) shares the same
        // node-location data instead of each re-running its own
        // IndexOf search.
        var flattenedNodesByOffset = new List<(CapabilityOutlineNodeDto Node, int Offset, int PageNumber)>();

        foreach (var chapter in result.Chapters)
        {
            foreach (var node in chapter.Nodes)
            {
                var marker = !string.IsNullOrEmpty(node.Content) ? node.Content : node.StartMarker;
                if (string.IsNullOrEmpty(marker))
                {
                    continue;
                }

                var offset = documentText.IndexOf(marker, StringComparison.Ordinal);
                if (offset < 0)
                {
                    continue;
                }

                var page = extraction.PageOffsets.FirstOrDefault(p => offset >= p.StartOffset && offset < p.EndOffset);
                flattenedNodesByOffset.Add((node, offset, page.PageNumber));
            }
        }

        // PASS 0 — TITLE MATCHING (2026-11-10 "Transformer attention image
        // landed under the previous/next chapter instead of its own
        // section" fix): the position-only heuristics below assume a
        // page's trailing images belong to whichever node is textually
        // "closest" to them — but that guess breaks whenever the real
        // owning node starts LATER on the same page (after earlier nodes)
        // or the image actually belongs to content that trails into the
        // NEXT chapter/page. Every embedded-image description the
        // extraction pipeline generates starts with its own title/heading
        // line (e.g. "# Worked Example: Linear Regression"), which very
        // often closely echoes the real section heading it illustrates.
        // Here each image's own title is compared against every node's
        // title in the WHOLE document (not just same-page neighbors) and,
        // when a confident match is found, the image is attached directly
        // to that node — regardless of position/chapter — before any
        // positional guess ever runs.
        foreach (var page in extraction.PageOffsets)
        {
            if (!extraction.PageImageDataUrls.TryGetValue(page.PageNumber, out var images) || images.Count == 0)
            {
                continue;
            }

            if (!imageMarkerOffsetsByPage.TryGetValue(page.PageNumber, out var markerOffsets))
            {
                markerOffsets = FindImageMarkerOffsets(documentText, page.PageNumber, page.StartOffset, page.EndOffset);
                imageMarkerOffsetsByPage[page.PageNumber] = markerOffsets;
            }

            for (var i = 0; i < markerOffsets.Count && i < images.Count; i++)
            {
                var blockEnd = i + 1 < markerOffsets.Count ? markerOffsets[i + 1] : page.EndOffset;
                var imageTitle = ExtractImageDescriptionTitle(documentText, markerOffsets[i], blockEnd);
                if (string.IsNullOrWhiteSpace(imageTitle))
                {
                    continue;
                }

                CapabilityOutlineNodeDto? bestNode = null;
                var bestScore = 0.0;
                foreach (var (candidateNode, _, _) in flattenedNodesByOffset)
                {
                    var score = TitleSimilarity(imageTitle, candidateNode.Title);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestNode = candidateNode;
                    }
                }

                // Require a strong, confident overlap (not just one
                // generic shared word) before trusting the title match
                // over the positional heuristics below.
                if (bestNode is null || bestScore < 0.6)
                {
                    continue;
                }

                if (!assignedImageIndexesByPage.TryGetValue(page.PageNumber, out var usedIndexes))
                {
                    usedIndexes = [];
                    assignedImageIndexesByPage[page.PageNumber] = usedIndexes;
                }

                if (usedIndexes.Add(i))
                {
                    bestNode.ImageDataUrls = [.. bestNode.ImageDataUrls ?? [], images[i]];
                }
            }
        }

        // PASS 1 — POSITIONAL (pre-existing heuristic, still the fallback
        // for images whose description title didn't confidently match any
        // node above): each distinct image on the page is matched to the
        // node whose own text falls right after that image's own
        // "[Descripción de imagen — página N]" marker in the page's
        // text (in document order), and is then handed out ONCE — every
        // later node that maps to the SAME image gets none, avoiding
        // duplicate thumbnails while still letting different images on
        // one page reach their own separate chapters/nodes.
        foreach (var (node, offset, pageNumber) in flattenedNodesByOffset)
        {
            if (pageNumber == 0)
            {
                continue;
            }

            if (!extraction.PageImageDataUrls.TryGetValue(pageNumber, out var images) || images.Count == 0)
            {
                continue;
            }

            if (!imageMarkerOffsetsByPage.TryGetValue(pageNumber, out var markerOffsets))
            {
                markerOffsets = FindImageMarkerOffsets(documentText, pageNumber, extraction.PageOffsets.First(p => p.PageNumber == pageNumber).StartOffset, extraction.PageOffsets.First(p => p.PageNumber == pageNumber).EndOffset);
                imageMarkerOffsetsByPage[pageNumber] = markerOffsets;
            }

            // Which image on this page does this node's own text belong
            // after? The last marker at or before the node's offset
            // (falling back to the first image if the node sits before
            // every marker, e.g. real page text preceding the images).
            var imageIndex = 0;
            for (var i = 0; i < markerOffsets.Count && markerOffsets[i] <= offset; i++)
            {
                imageIndex = i;
            }

            if (!assignedImageIndexesByPage.TryGetValue(pageNumber, out var usedIndexes))
            {
                usedIndexes = [];
                assignedImageIndexesByPage[pageNumber] = usedIndexes;
            }

            if (usedIndexes.Add(imageIndex) && imageIndex < images.Count)
            {
                node.ImageDataUrls = [images[imageIndex]];
            }
        }

        // CONTINUATION PAGES (2026-11-10 "page 10's regression charts never
        // reached the Step-by-step calculation engram" fix; widened
        // 2026-11-10 to also cover a page that DOES have its own node
        // start): a long node (e.g. a worked example/calculation
        // walkthrough) can start on one page and keep going — together
        // with its own figures — onto the NEXT page. Those figures'
        // "[Descripción de imagen — página N]" markers are appended by the
        // extraction pipeline AFTER ALL of that page's real text — so when
        // a LATER heading (e.g. "Interpretation and limitations") also
        // starts on that same page, before any image marker, the per-node
        // loop above has no marker to disambiguate and simply hands
        // whichever image(s) it can to that later heading, starving the
        // earlier node whose own worked example the figures actually
        // illustrate (its own slice textually contains the "Figure: ..."
        // captions, even though the image markers themselves sit past the
        // later heading). Rather than requiring the page to have NO node
        // start at all, this now runs for every page with any image left
        // unclaimed by the per-node loop above, and hands the leftovers to
        // the owning node — the last node (in document order) whose own
        // offset is at or before the page's START (i.e. the node that was
        // already "open" when this page began, never the later heading
        // that merely also happens to start somewhere mid-page).
        flattenedNodesByOffset.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        foreach (var page in extraction.PageOffsets)
        {
            if (!extraction.PageImageDataUrls.TryGetValue(page.PageNumber, out var images) || images.Count == 0)
            {
                continue;
            }

            assignedImageIndexesByPage.TryGetValue(page.PageNumber, out var alreadyClaimed);
            var leftoverIndexes = Enumerable.Range(0, images.Count)
                .Where(i => alreadyClaimed is null || !alreadyClaimed.Contains(i))
                .ToList();
            if (leftoverIndexes.Count == 0)
            {
                continue;
            }

            CapabilityOutlineNodeDto? owningNode = null;
            foreach (var (node, offset, _) in flattenedNodesByOffset)
            {
                if (offset > page.StartOffset)
                {
                    break;
                }
                owningNode = node;
            }

            if (owningNode is null)
            {
                continue;
            }

            assignedImageIndexesByPage[page.PageNumber] = [.. (alreadyClaimed ?? []), .. leftoverIndexes];
            owningNode.ImageDataUrls = [.. owningNode.ImageDataUrls ?? [], .. leftoverIndexes.Select(i => images[i])];
        }

        // FALLBACK (2026-11-05): the exact-match pass above requires a
        // node's own Content/StartMarker to appear character-for-character
        // in documentText — but the direct-extraction tier is instructed to
        // transcribe an image's OWN internal sections (headings, bullets)
        // rather than repeat the raw "[Descripción de imagen — página N]"
        // block verbatim, so its lightly-reformatted engram text can fail
        // the ordinal IndexOf search entirely. When that happens every node
        // of a whole image-only chapter can end up with NO image at all,
        // even though a real, still-unclaimed image exists for it. Here we
        // hand out every image the exact-match pass left unclaimed, in
        // page/document order, to the first node of each chapter that
        // still has zero images, in chapter order — a best-effort pairing
        // rather than leaving a real chapter with no image whatsoever.
        var unassignedImages = new List<string>();
        foreach (var pageNumber in extraction.PageImageDataUrls.Keys.OrderBy(p => p))
        {
            var images = extraction.PageImageDataUrls[pageNumber];
            assignedImageIndexesByPage.TryGetValue(pageNumber, out var usedIndexes);
            for (var i = 0; i < images.Count; i++)
            {
                if (usedIndexes is null || !usedIndexes.Contains(i))
                {
                    unassignedImages.Add(images[i]);
                }
            }
        }

        if (unassignedImages.Count > 0)
        {
            var fallbackIndex = 0;
            foreach (var chapter in result.Chapters)
            {
                if (fallbackIndex >= unassignedImages.Count)
                {
                    break;
                }

                var firstNode = chapter.Nodes.FirstOrDefault();
                if (firstNode is null || chapter.Nodes.Any(n => n.ImageDataUrls is { Count: > 0 }))
                {
                    continue;
                }

                firstNode.ImageDataUrls = [unassignedImages[fallbackIndex]];
                fallbackIndex++;
            }
        }
    }

    /// <summary>Finds, within one page's own text range, the start offset
    /// of each "[Descripción de imagen — página N]" marker inserted by
    /// <see cref="PdfContentExtractionService"/> — these appear in the
    /// same order as that page's <see cref="PdfContentExtractionService.ExtractionResult.PageImageDataUrls"/>
    /// entries, letting <see cref="AttachRealImages"/> pair each node back
    /// to the specific image it actually describes instead of the page's
    /// image list as a whole.</summary>
    private static List<int> FindImageMarkerOffsets(string documentText, int pageNumber, int pageStartOffset, int pageEndOffset)
    {
        var marker = $"[Descripción de imagen — página {pageNumber}]";
        var offsets = new List<int>();
        var searchFrom = pageStartOffset;
        while (searchFrom < pageEndOffset)
        {
            var found = documentText.IndexOf(marker, searchFrom, pageEndOffset - searchFrom, StringComparison.Ordinal);
            if (found < 0)
            {
                break;
            }

            offsets.Add(found);
            searchFrom = found + marker.Length;
        }

        return offsets;
    }

    /// <summary>Pulls the "title" line out of one embedded image's own
    /// AI-generated description block (the text between its
    /// "[Descripción de imagen — página N]" marker and the next marker/
    /// end of page) — used by <see cref="AttachRealImages"/>'s title-
    /// matching pass (2026-11-10) to pair an image back to the node whose
    /// heading it actually illustrates, regardless of position. The
    /// description pipeline emits either a "# Título" heading followed by
    /// the real title on its own line, or the real title directly as the
    /// first "# " heading — both forms are handled here.</summary>
    private static string? ExtractImageDescriptionTitle(string documentText, int markerOffset, int blockEnd)
    {
        var safeEnd = Math.Min(blockEnd, documentText.Length);
        if (safeEnd <= markerOffset)
        {
            return null;
        }

        var block = documentText[markerOffset..safeEnd];
        var lines = block.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('#'))
            {
                continue;
            }

            var heading = line.TrimStart('#').Trim();
            if (heading.Equals("Título", StringComparison.OrdinalIgnoreCase))
            {
                for (var j = i + 1; j < lines.Length; j++)
                {
                    var candidate = lines[j].Trim();
                    if (candidate.Length > 0)
                    {
                        return candidate;
                    }
                }

                return null;
            }

            if (heading.Length > 0
                && !heading.Equals("Resumen Ejecutivo", StringComparison.OrdinalIgnoreCase)
                && !heading.Equals("Índice de Contenidos", StringComparison.OrdinalIgnoreCase))
            {
                return heading;
            }
        }

        return null;
    }

    private static readonly HashSet<string> TitleMatchStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "of", "in", "to", "for", "on", "with", "is", "are", "this", "that",
        "your", "de", "la", "el", "los", "las", "y", "en", "con", "para", "un", "una",
    };

    /// <summary>Lowercased, punctuation-stripped, stop-word-filtered token
    /// set for one title — the shared vocabulary <see cref="TitleSimilarity"/>
    /// compares between an image's own description title and a candidate
    /// node's title.</summary>
    private static HashSet<string> TokenizeTitle(string title)
    {
        return [.. title
            .Split([' ', '\t', ':', ',', '.', '-', '—', '(', ')', '/', '“', '”', '"', '¿', '?', '¡', '!'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => w.Length > 2 && !TitleMatchStopWords.Contains(w))];
    }

    /// <summary>Containment-coefficient similarity (shared significant
    /// words ÷ size of the smaller title's vocabulary) between an image's
    /// description title and a node's title — 1.0 when the shorter title
    /// is fully covered by the longer one (e.g. image title "Worked
    /// Example: Linear Regression" inside node title "Worked Example:
    /// Predicting an Exam Score with Linear Regression"), 0 when they
    /// share nothing meaningful.</summary>
    private static double TitleSimilarity(string imageTitle, string nodeTitle)
    {
        var imageTokens = TokenizeTitle(imageTitle);
        var nodeTokens = TokenizeTitle(nodeTitle);
        if (imageTokens.Count == 0 || nodeTokens.Count == 0)
        {
            return 0;
        }

        var intersectionCount = imageTokens.Count(t => nodeTokens.Contains(t));

        // A single shared generic word (e.g. both titles happen to say
        // "model") shouldn't be enough to confidently reroute an image —
        // require at least two overlapping significant words.
        if (intersectionCount < 2)
        {
            return 0;
        }

        var smaller = Math.Min(imageTokens.Count, nodeTokens.Count);
        return (double)intersectionCount / smaller;
    }
}
