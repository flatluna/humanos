using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class AddStructuredLearningSourceFrameRequestDto
{
    /// <summary>Raw image bytes, base64-encoded (PNG/JPEG).</summary>
    public string ContentBase64 { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/png";
    public string? Caption { get; set; }
    /// <summary>Optional — where in the original source video this image
    /// corresponds to, shown alongside the other real frames' timestamps.</summary>
    public double? VideoTimestampSeconds { get; set; }
}

public sealed class AddStructuredLearningSourceFrameResponseDto
{
    public Guid IllustrationId { get; set; }
}

/// <summary>
/// Lets a designer ADD a new real image (Purpose=SourceDocument) to a section in
/// edit mode (2026-11-07) — saved ALONGSIDE the existing source frames (never
/// replacing them), so it is picked up the next time "Restart engrams" regenerates
/// this section's per-frame narration, exactly like a frame that had been extracted
/// from the original video. Counterpart to DeleteStructuredLearningSourceFrameFunction.
/// </summary>
public sealed class AddStructuredLearningSourceFrameFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    public AddStructuredLearningSourceFrameFunction(
        HumanOsDbContext dbContext,
        CapabilityGraphIllustrationStorageService illustrationStorage)
    {
        _dbContext = dbContext;
        _illustrationStorage = illustrationStorage;
    }

    [Function("AddStructuredLearningSourceFrame")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/source-frames")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        AddStructuredLearningSourceFrameRequestDto? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<AddStructuredLearningSourceFrameRequestDto>(
                request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequestBody", "Could not parse the request body.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.ContentBase64))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingImageContent", "ContentBase64 is required.", cancellationToken);
        }

        if (!_illustrationStorage.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "StorageNotConfigured", "Image storage is not configured.", cancellationToken);
        }

        var section = await _dbContext.StructuredLearningDocumentSections
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken);
        if (section is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SectionNotFound", $"Section '{sectionId}' was not found.", cancellationToken);
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(body.ContentBase64);
        }
        catch (FormatException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidImageContent", "ContentBase64 is not valid base64.", cancellationToken);
        }

        // Offset (500+) keeps manually-added frames out of the way of the
        // 200+ range used by the self-healing SourceDocument backfill in
        // StructuredLearningProfessorLectureService, avoiding blob-path collisions.
        var existingCount = await _dbContext.StructuredLearningIllustrations
            .CountAsync(i => i.StructuredLearningDocumentSectionId == sectionId
                && i.Purpose == StructuredLearningIllustrationPurpose.SourceDocument, cancellationToken);

        await using var imageStream = new MemoryStream(imageBytes);
        var storagePath = await _illustrationStorage.UploadIllustrationAsync(
            section.TenantId,
            section.StructuredLearningDocumentId,
            section.StructuredLearningDocumentSectionId,
            500 + existingCount,
            imageStream,
            string.IsNullOrWhiteSpace(body.ContentType) ? "image/png" : body.ContentType,
            cancellationToken);

        var illustration = new StructuredLearningIllustration
        {
            StructuredLearningIllustrationId = Guid.NewGuid(),
            StructuredLearningDocumentSectionId = sectionId,
            StoragePath = storagePath,
            Prompt = "Manually added source-document image (uploaded via Studio edit mode)",
            Caption = string.IsNullOrWhiteSpace(body.Caption) ? null : body.Caption,
            Purpose = StructuredLearningIllustrationPurpose.SourceDocument,
            ImageModel = "manual-upload",
            VideoTimestampSeconds = body.VideoTimestampSeconds,
            CreatedDate = DateTime.UtcNow
        };
        _dbContext.StructuredLearningIllustrations.Add(illustration);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new AddStructuredLearningSourceFrameResponseDto
        {
            IllustrationId = illustration.StructuredLearningIllustrationId
        }, HttpStatusCode.Created, cancellationToken: cancellationToken);
    }
}
