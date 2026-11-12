using System.Security.Cryptography;
using HumanOS.Agents.Studio;
using HumanOS.Contracts.ProcessDocuments;
using HumanOS.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HumanOS.Services.ProcessDocuments;

public sealed class ProcessDocumentValidationException : Exception
{
    public string ErrorCode { get; }

    public ProcessDocumentValidationException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}

public sealed class ProcessDocumentUnauthorizedException : Exception
{
    public ProcessDocumentUnauthorizedException(string message) : base(message) { }
}

public sealed class ProcessDocumentPipelineService
{
    public const long DefaultMaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB

    private readonly IProcessDocumentStorageService _storageService;
    private readonly IProcessDocumentExtractionService _extractionService;
    private readonly IConfiguration _configuration;
    private readonly ProcessImageStepMatcherAgent? _imageStepMatcherAgent;
    private readonly ILogger<ProcessDocumentPipelineService>? _logger;

    // Cache of recent completed uploads for idempotency / duplicate retry detection
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ProcessDocumentUploadResponse>
        IdempotencyCache = new();

    public ProcessDocumentPipelineService(
        IProcessDocumentStorageService storageService,
        IProcessDocumentExtractionService extractionService,
        IConfiguration configuration,
        ProcessImageStepMatcherAgent? imageStepMatcherAgent = null,
        ILogger<ProcessDocumentPipelineService>? logger = null)
    {
        _storageService = storageService;
        _extractionService = extractionService;
        _configuration = configuration;
        _imageStepMatcherAgent = imageStepMatcherAgent;
        _logger = logger;
    }

    public long MaxFileSizeBytes
    {
        get
        {
            var configValue = _configuration["ProcessDocument:MaxFileSizeBytes"];
            return long.TryParse(configValue, out var parsed) && parsed > 0
                ? parsed
                : DefaultMaxFileSizeBytes;
        }
    }

    public async Task<ProcessDocumentUploadResponse> ProcessAsync(
        byte[] fileBytes,
        string fileName,
        string? contentType,
        string? userId,
        string? organizationId,
        string? processName = null,
        string? processDescription = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Authenticate & Authorize
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(organizationId))
        {
            throw new ProcessDocumentUnauthorizedException(
                "Authentication required: both user ID and organization ID must be provided.");
        }

        var normalizedOrgId = organizationId.Trim().ToLowerInvariant();
        var normalizedUserId = userId.Trim();

        // 2. Validate PDF file
        ValidatePdf(fileBytes, fileName, contentType);

        // 3. Validate file size
        if (fileBytes.Length > MaxFileSizeBytes)
        {
            throw new ProcessDocumentValidationException(
                "OversizedFile",
                $"The uploaded file exceeds the configured maximum size of {MaxFileSizeBytes / (1024 * 1024)}MB. Actual: {fileBytes.Length} bytes.");
        }

        // 4. Duplicate / retry check via content hash + organization + processName
        var contentHash = Convert.ToHexString(SHA256.HashData(fileBytes));
        var idempotencyKey = $"{normalizedOrgId}:{processName?.Trim().ToLowerInvariant() ?? ""}:{contentHash}";

        if (IdempotencyCache.TryGetValue(idempotencyKey, out var existingResponse))
        {
            if (await _storageService.HasDocumentAsync(normalizedOrgId, existingResponse.DocumentId, cancellationToken))
            {
                _logger?.LogInformation("Duplicate/retried request detected for key {Key}. Returning cached result.", idempotencyKey);
                return existingResponse;
            }
            IdempotencyCache.TryRemove(idempotencyKey, out _);
        }

        // 5. Generate unique document ID
        var documentId = Guid.NewGuid().ToString("D").ToLowerInvariant();
        var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "original.pdf" : Path.GetFileName(fileName);

        // 6. Save original PDF in Azure Data Lake Storage
        string sourceStoragePath;
        try
        {
            using var pdfStream = new MemoryStream(fileBytes);
            sourceStoragePath = await _storageService.UploadSourcePdfAsync(
                normalizedOrgId,
                documentId,
                safeFileName,
                pdfStream,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Failed to upload original PDF to Data Lake Storage for document {DocumentId}", documentId);
            throw new InvalidOperationException($"Storage failure during PDF upload: {ex.Message}", ex);
        }

        // 7. Extract text, tables, headings, and document layout + Transform to structured JSON
        RawDocumentExtractionResult rawResult;
        ProcessDocumentStructure structuredResult;

        try
        {
            (rawResult, structuredResult) = await _extractionService.ExtractAndStructureAsync(
                fileBytes,
                safeFileName,
                documentId,
                processName,
                processDescription,
                sourceStoragePath,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Document processing failure during extraction for document {DocumentId}", documentId);
            throw new InvalidOperationException($"Document-processing failure: {ex.Message}", ex);
        }

        // 7.5. Extract real embedded images (screenshots/diagrams/figures) per page and attach them
        // to whichever step(s) cover that page — best-effort only: a failure here must never block
        // saving the text-based structure, since images are a value-add, not the core extraction.
        try
        {
            await AttachStepImagesAsync(fileBytes, normalizedOrgId, documentId, structuredResult, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Best-effort image extraction failed for document {DocumentId}; continuing without step images.", documentId);
        }

        // 8. Save raw extraction JSON to Azure Data Lake Storage
        try
        {
            await _storageService.SaveRawExtractionAsync(
                normalizedOrgId,
                documentId,
                rawResult,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Storage failure saving raw extraction JSON for document {DocumentId}", documentId);
            throw new InvalidOperationException($"Storage failure during raw extraction persistence: {ex.Message}", ex);
        }

        // 9. Save structured JSON to Azure Data Lake Storage
        string structuredResultPath;
        try
        {
            structuredResultPath = await _storageService.SaveStructuredResultAsync(
                normalizedOrgId,
                documentId,
                structuredResult,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Storage failure saving structured JSON for document {DocumentId}", documentId);
            throw new InvalidOperationException($"Storage failure during structured result persistence: {ex.Message}", ex);
        }

        // 10. Build typed response
        var totalSteps = structuredResult.Chapters.Sum(c => c.Steps.Count);
        var totalTables = structuredResult.Tables.Count;

        var response = new ProcessDocumentUploadResponse
        {
            DocumentId = documentId,
            FileName = safeFileName,
            ProcessName = structuredResult.ProcessName,
            Status = "completed",
            SourceStoragePath = sourceStoragePath,
            StructuredResultPath = structuredResultPath,
            ChaptersIdentified = structuredResult.Chapters.Count,
            StepsIdentified = totalSteps,
            TablesExtracted = totalTables,
            Warnings = structuredResult.Warnings
        };

        // Cache for idempotency
        IdempotencyCache.TryAdd(idempotencyKey, response);

        return response;
    }

    /// <summary>Extracts every real embedded image from the original PDF (via
    /// <see cref="PdfTextExtractor.ExtractPagesWithImages"/>, which already filters out decorative
    /// icons/full-bleed backgrounds), uploads each one to blob storage, and attaches it to every
    /// <see cref="ProcessStep"/> whose <see cref="ProcessStep.SourcePages"/> includes that image's page —
    /// so a figure appearing on the same page as a step's text ends up alongside that step's
    /// verbatim source_content in the Review Process Steps screen.</summary>
    private async Task AttachStepImagesAsync(
        byte[] fileBytes,
        string normalizedOrgId,
        string documentId,
        ProcessDocumentStructure structuredResult,
        CancellationToken cancellationToken)
    {
        using var pdfStream = new MemoryStream(fileBytes);
        var pages = PdfTextExtractor.ExtractPagesWithImages(pdfStream);
        if (pages.All(p => p.Images.Count == 0))
        {
            return;
        }

        var allSteps = structuredResult.Chapters.SelectMany(c => c.Steps).ToList();

        foreach (var page in pages)
        {
            if (page.Images.Count == 0) continue;

            var stepsOnThisPage = allSteps.Where(s => s.SourcePages.Contains(page.PageNumber)).ToList();
            if (stepsOnThisPage.Count == 0) continue;

            for (var i = 0; i < page.Images.Count; i++)
            {
                var image = page.Images[i];
                var extension = image.ContentType == "image/jpeg" ? "jpg" : "png";
                var blobFileName = $"page-{page.PageNumber}-img-{i + 1}.{extension}";

                string storagePath;
                try
                {
                    using var imageStream = new MemoryStream(image.Bytes);
                    storagePath = await _storageService.UploadStepImageAsync(
                        normalizedOrgId, documentId, blobFileName, imageStream, image.ContentType, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger?.LogWarning(ex, "Failed to upload step image {BlobFileName} for document {DocumentId}", blobFileName, documentId);
                    continue;
                }

                var stepImage = new ProcessStepImage
                {
                    SourcePage = page.PageNumber,
                    StoragePath = storagePath,
                    ContentType = image.ContentType,
                    DataUrl = $"data:{image.ContentType};base64,{Convert.ToBase64String(image.Bytes)}"
                };

                // If the page is shared by more than one step, page-number matching alone can't tell
                // which step the image actually illustrates (e.g. a short page with the tail of one
                // step and the start of the next). Ask the vision matcher agent to decide; fail open
                // to "attach to every step on the page" (today's behavior) if it's unavailable or errors.
                var targetSteps = stepsOnThisPage;
                if (stepsOnThisPage.Count > 1 && _imageStepMatcherAgent is { IsConfigured: true })
                {
                    try
                    {
                        var candidates = stepsOnThisPage
                            .Select(s => new ProcessImageStepMatcherAgent.StepCandidate
                            {
                                StepId = s.StepId,
                                StepName = s.StepName,
                                SourceContent = s.SourceContent
                            })
                            .ToList();

                        var match = await _imageStepMatcherAgent.MatchAsync(
                            image.Bytes, image.ContentType, candidates, cancellationToken);

                        if (match.MatchedStepIds.Count > 0)
                        {
                            targetSteps = stepsOnThisPage
                                .Where(s => match.MatchedStepIds.Contains(s.StepId))
                                .ToList();
                        }
                        else
                        {
                            // Agent confidently says this image belongs to none of the candidates
                            // (e.g. decorative) — skip attaching it anywhere rather than spamming
                            // every step on the page.
                            targetSteps = [];
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger?.LogWarning(ex, "Image-step matching failed for page {PageNumber} of document {DocumentId}; keeping page-level association.", page.PageNumber, documentId);
                    }
                }

                foreach (var step in targetSteps)
                {
                    step.StepImages.Add(stepImage);
                }
            }
        }
    }

    public static void ValidatePdf(byte[] fileBytes, string? fileName, string? contentType)
    {
        if (fileBytes == null || fileBytes.Length == 0)
        {
            throw new ProcessDocumentValidationException("EmptyFile", "The uploaded file is empty.");
        }

        // Check file extension
        var hasPdfExtension = !string.IsNullOrWhiteSpace(fileName) &&
                              fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

        // Check content type
        var hasPdfContentType = !string.IsNullOrWhiteSpace(contentType) &&
                                contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase);

        // Check PDF magic bytes (%PDF- = 0x25, 0x50, 0x44, 0x46, 0x2D)
        var hasPdfMagicHeader = fileBytes.Length >= 5 &&
                                fileBytes[0] == 0x25 && // %
                                fileBytes[1] == 0x50 && // P
                                fileBytes[2] == 0x44 && // D
                                fileBytes[3] == 0x46 && // F
                                fileBytes[4] == 0x2D;   // -

        if (!hasPdfMagicHeader && !hasPdfExtension && !hasPdfContentType)
        {
            throw new ProcessDocumentValidationException(
                "NonPdfFile",
                "Only PDF files are accepted. The uploaded file does not have a PDF header or extension.");
        }

        if (!hasPdfMagicHeader && !hasPdfExtension)
        {
            throw new ProcessDocumentValidationException(
                "InvalidPdfHeader",
                "The file signature does not match a valid PDF document (%PDF- magic header missing).");
        }
    }
}
