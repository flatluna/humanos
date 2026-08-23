using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Serves the actual image bytes of a StructuredLearningIllustration —
/// same "resolve reference in SQL, then stream blob" split as
/// GetIllustrationImageFunction/GetTraditionalIllustrationImageFunction,
/// just pointed at the Structured Learning illustrations table instead.
/// </summary>
public sealed class GetStructuredLearningIllustrationImageFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;

    public GetStructuredLearningIllustrationImageFunction(HumanOsDbContext dbContext, CapabilityGraphIllustrationStorageService illustrationStorage)
    {
        _dbContext = dbContext;
        _illustrationStorage = illustrationStorage;
    }

    [Function("GetStructuredLearningIllustrationImage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "illustrations/structured-learning/{illustrationId:guid}/image")]
        HttpRequestData request,
        Guid illustrationId,
        CancellationToken cancellationToken)
    {
        if (!_illustrationStorage.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured",
                "CapabilityGraphIllustrationStorageService is not configured (missing 'DataLakeStorage' connection string).",
                cancellationToken);
        }

        var illustration = await _dbContext.StructuredLearningIllustrations
            .AsNoTracking()
            .Where(i => i.StructuredLearningIllustrationId == illustrationId)
            .Select(i => new { i.StoragePath, i.MediaType })
            .FirstOrDefaultAsync(cancellationToken);

        if (illustration is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "IllustrationNotFound",
                $"No StructuredLearningIllustration found with id {illustrationId}.", cancellationToken);
        }

        try
        {
            var content = await _illustrationStorage.DownloadIllustrationBytesAsync(illustration.StoragePath, cancellationToken);
            var bytes = content.ToMemory();
            var contentType = illustration.MediaType == "Video" ? "video/mp4" : "image/png";

            // Videos need real Content-Length + Range support (2026-08-22):
            // Google's .mp4 files often aren't "faststart" (the 'moov' atom
            // with track metadata sits at the END of the file), and without
            // Accept-Ranges/Content-Length the browser's <video> element
            // reliably fails to recognize the audio track — it plays the
            // video but the volume control stays disabled, even though the
            // file genuinely contains a real audio stream.
            var rangeHeader = request.Headers.TryGetValues("Range", out var rangeValues) ? rangeValues.FirstOrDefault() : null;
            if (rangeHeader is not null && TryParseRange(rangeHeader, bytes.Length, out var start, out var end))
            {
                var slice = bytes.Slice(start, end - start + 1);
                var partial = request.CreateResponse(HttpStatusCode.PartialContent);
                partial.Headers.Add("Content-Type", contentType);
                partial.Headers.Add("Cache-Control", "public, max-age=31536000, immutable");
                partial.Headers.Add("Accept-Ranges", "bytes");
                partial.Headers.Add("Content-Range", $"bytes {start}-{end}/{bytes.Length}");
                partial.Headers.Add("Content-Length", slice.Length.ToString());
                await partial.Body.WriteAsync(slice, cancellationToken);
                return partial;
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", contentType);
            response.Headers.Add("Cache-Control", "public, max-age=31536000, immutable");
            response.Headers.Add("Accept-Ranges", "bytes");
            response.Headers.Add("Content-Length", bytes.Length.ToString());
            await response.Body.WriteAsync(bytes, cancellationToken);
            return response;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ImageBlobNotFound",
                $"StoragePath '{illustration.StoragePath}' has no corresponding blob in Data Lake.", cancellationToken);
        }
    }

    /// <summary>Parses a single-range "bytes=start-end" Range header (the
    /// only form browsers send for video seeking) — returns false for
    /// anything else (multi-range, suffix-only, malformed) so the caller
    /// falls back to a full 200 response.</summary>
    private static bool TryParseRange(string rangeHeader, int totalLength, out int start, out int end)
    {
        start = 0;
        end = totalLength - 1;
        if (!rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) return false;

        var parts = rangeHeader["bytes=".Length..].Split('-');
        if (parts.Length != 2) return false;

        if (!string.IsNullOrEmpty(parts[0]) && !int.TryParse(parts[0], out start)) return false;
        if (!string.IsNullOrEmpty(parts[1]) && int.TryParse(parts[1], out var parsedEnd)) end = parsedEnd;

        end = Math.Min(end, totalLength - 1);
        return start >= 0 && start <= end;
    }
}

