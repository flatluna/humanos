using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetVideoProductionClipAnchorImageResponse
{
    public string Base64 { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/png";
}

/// <summary>Returns the exact starting frame image a clip was generated
/// from (2026-09-24) — clip 0's own uploaded/edited image, or the previous
/// clip's last frame carried over for continuity. Lets reopening a
/// project restore the "Starting frame for this clip" preview instead of
/// only ever showing it during the original session.</summary>
public sealed class GetVideoProductionClipAnchorImageFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _storage;

    public GetVideoProductionClipAnchorImageFunction(HumanOsDbContext dbContext, CapabilityGraphIllustrationStorageService storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    [Function("GetVideoProductionClipAnchorImage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/video-productions/clips/{clipId:guid}/anchor-image")]
        HttpRequestData request,
        Guid clipId,
        CancellationToken cancellationToken)
    {
        var clip = await _dbContext.VideoProductionClips
            .AsNoTracking()
            .Where(c => c.VideoProductionClipId == clipId)
            .Select(c => c.AnchorImageStoragePath)
            .FirstOrDefaultAsync(cancellationToken);

        if (clip is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "AnchorImageNotFound", "This clip has no saved starting frame.", cancellationToken);
        }

        var bytes = await _storage.DownloadIllustrationBytesAsync(clip, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new GetVideoProductionClipAnchorImageResponse { Base64 = Convert.ToBase64String(bytes.ToArray()) },
            cancellationToken: cancellationToken);
    }
}
