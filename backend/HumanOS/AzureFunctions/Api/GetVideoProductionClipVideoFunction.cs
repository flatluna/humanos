using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Streams one Video Production clip's generated video bytes.</summary>
public sealed class GetVideoProductionClipVideoFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _storage;

    public GetVideoProductionClipVideoFunction(HumanOsDbContext dbContext, CapabilityGraphIllustrationStorageService storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    [Function("GetVideoProductionClipVideo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/video-productions/clips/{clipId:guid}/video")]
        HttpRequestData request,
        Guid clipId,
        CancellationToken cancellationToken)
    {
        if (!_storage.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured",
                "CapabilityGraphIllustrationStorageService is not configured (missing 'DataLakeStorage' connection string).",
                cancellationToken);
        }

        var clip = await _dbContext.VideoProductionClips
            .AsNoTracking()
            .Where(c => c.VideoProductionClipId == clipId)
            .Select(c => new { c.StoragePath })
            .FirstOrDefaultAsync(cancellationToken);

        if (clip?.StoragePath is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ClipVideoNotFound", $"No video found for clip {clipId}.", cancellationToken);
        }

        var content = await _storage.DownloadIllustrationBytesAsync(clip.StoragePath, cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "video/mp4");
        response.Headers.Add("Cache-Control", "public, max-age=31536000, immutable");
        await response.Body.WriteAsync(content.ToMemory(), cancellationToken);
        return response;
    }
}
