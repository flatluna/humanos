using System.Net;
using System.Text.Json;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Extracts plain text from a PDF the user attaches as raw material during
/// Human OS Studio capability creation (see ObjectiveStep.tsx's
/// MaterialUploader). Reuses the existing <see cref="PdfTextExtractor"/>
/// (UglyToad.PdfPig) that already serves Job Description PDF uploads. The
/// extracted text is returned to the caller, which folds it into a
/// RawMaterialItem { Type = Pdf, Content = text } and sends it along with
/// StartCapabilityCreationFunction's rawMaterials, exactly like the
/// existing .txt/.md client-side-read path.
///
/// The original PDF bytes are ALSO persisted to
/// <see cref="CapabilityMaterialStorageService"/> (its own tenant-scoped
/// Data Lake container, separate from role-documents/job-descriptions) so
/// the source file isn't lost after this stateless extraction call — best
/// effort: if Data Lake storage isn't configured for this environment,
/// extraction still succeeds and StoragePath is simply null.
/// </summary>
public sealed class ExtractCapabilityMaterialPdfFunction
{
    private readonly CapabilityMaterialStorageService _storageService;

    public ExtractCapabilityMaterialPdfFunction(CapabilityMaterialStorageService storageService)
    {
        _storageService = storageService;
    }

    public sealed class ExtractRequest
    {
        public Guid TenantId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string ContentBase64 { get; set; } = string.Empty;
    }

    public sealed class ExtractResponse
    {
        public string FileName { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        /// <summary>Blob path within the tenant's capability-materials
        /// container, or null if Data Lake storage isn't configured for
        /// this environment (extraction still succeeds either way).</summary>
        public string? StoragePath { get; set; }
    }

    [Function("ExtractCapabilityMaterialPdf")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/capability-creation/materials/extract-pdf")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        ExtractRequest? extractRequest;

        try
        {
            extractRequest = await JsonSerializer.DeserializeAsync<ExtractRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (extractRequest is null || extractRequest.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(extractRequest.ContentBase64))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "RequestBodyRequired", "'tenantId', 'fileName' and 'contentBase64' are required.", cancellationToken);
        }

        byte[] fileBytes;

        try
        {
            fileBytes = Convert.FromBase64String(extractRequest.ContentBase64);
        }
        catch (FormatException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidFileContent", "The file content is not valid base64.", cancellationToken);
        }

        string text;

        try
        {
            using var contentStream = new MemoryStream(fileBytes);
            text = PdfTextExtractor.ExtractText(contentStream);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "PdfExtractionFailed",
                $"Could not extract text from '{extractRequest.FileName}'. Is it a valid, non-encrypted PDF?",
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.UnprocessableEntity,
                "PdfHasNoExtractableText",
                $"'{extractRequest.FileName}' has no extractable text (it may be a scanned/image-only PDF).",
                cancellationToken);
        }

        string? storagePath = null;

        if (_storageService.IsConfigured)
        {
            try
            {
                using var uploadStream = new MemoryStream(fileBytes);
                storagePath = await _storageService.UploadAsync(
                    extractRequest.TenantId, extractRequest.FileName, uploadStream, "application/pdf", cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Persisting the original file is best-effort — the caller
                // already has the (more valuable) extracted text, so don't
                // fail the whole request over a storage hiccup.
                storagePath = null;
            }
        }

        var response = new ExtractResponse { FileName = extractRequest.FileName, Text = text, StoragePath = storagePath };
        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }
}
