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
    private readonly IConfiguration _configuration;

    public PreviewCapabilityOutlineFunction(
        TocExtractionAgent tocExtraction,
        PdfImageDescriptionAgent pdfImageDescription,
        PdfContentExtractionService pdfContentExtraction,
        IConfiguration configuration)
    {
        _tocExtraction = tocExtraction;
        _pdfImageDescription = pdfImageDescription;
        _pdfContentExtraction = pdfContentExtraction;
        _configuration = configuration;
    }

    public sealed class PreviewRequest
    {
        /// <summary>Required when previewing an uploaded PDF — same
        /// base64-body convention as StartPdfCapabilityGraphFunction.
        /// Mutually exclusive with <see cref="Text"/>.</summary>
        public string? FileName { get; set; }

        public string? ContentBase64 { get; set; }

        /// <summary>Required when previewing plain text instead of a PDF
        /// (the "Texto/idea" entry point, or a pasted table of contents) —
        /// mutually exclusive with <see cref="ContentBase64"/>.</summary>
        public string? Text { get; set; }
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

        string documentText;
        if (hasPdf)
        {
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
        }
        else
        {
            documentText = body.Text!;
        }

        if (string.IsNullOrWhiteSpace(documentText))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "EmptyDocument", "No extractable text was found.", cancellationToken);
        }

        var result = await CapabilityOutlinePreviewService.PreviewAsync(documentText, _tocExtraction, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
    }
}
