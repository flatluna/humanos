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

        if (body is null || (!hasPdf && !hasText))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "InvalidRequest",
                "Either ('fileName' + 'contentBase64') or 'text' is required.",
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
                cancellationToken);

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

        var result = await CapabilityOutlinePreviewService.PreviewAsync(documentText, _tocExtraction, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
    }
}
