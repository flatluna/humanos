using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Streams the merged final video for a VideoProduction.</summary>
public sealed class GetVideoProductionVideoFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _storage;

    public GetVideoProductionVideoFunction(HumanOsDbContext dbContext, CapabilityGraphIllustrationStorageService storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    [Function("GetVideoProductionVideo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/video-productions/{videoProductionId:guid}/video")]
        HttpRequestData request,
        Guid videoProductionId,
        CancellationToken cancellationToken)
    {
        if (!_storage.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured",
                "CapabilityGraphIllustrationStorageService is not configured (missing 'DataLakeStorage' connection string).",
                cancellationToken);
        }

        var production = await _dbContext.VideoProductions
            .AsNoTracking()
            .Where(p => p.VideoProductionId == videoProductionId)
            .Select(p => new { p.FinalVideoStoragePath })
            .FirstOrDefaultAsync(cancellationToken);

        if (production?.FinalVideoStoragePath is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "VideoNotFound",
                $"No merged video found for VideoProduction {videoProductionId}.", cancellationToken);
        }

        var content = await _storage.DownloadIllustrationBytesAsync(production.FinalVideoStoragePath, cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "video/mp4");
        response.Headers.Add("Cache-Control", "public, max-age=31536000, immutable");
        await response.Body.WriteAsync(content.ToMemory(), cancellationToken);
        return response;
    }
}
