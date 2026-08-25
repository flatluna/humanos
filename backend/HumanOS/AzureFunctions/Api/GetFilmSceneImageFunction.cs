using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Serves the final composited image bytes for one FilmScene —
/// same "resolve reference in SQL, then stream blob" split as
/// GetStructuredLearningIllustrationImageFunction.</summary>
public sealed class GetFilmSceneImageFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _storage;

    public GetFilmSceneImageFunction(HumanOsDbContext dbContext, CapabilityGraphIllustrationStorageService storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    [Function("GetFilmSceneImage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/film-productions/scenes/{sceneId:guid}/image")]
        HttpRequestData request,
        Guid sceneId,
        CancellationToken cancellationToken)
    {
        if (!_storage.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured",
                "CapabilityGraphIllustrationStorageService is not configured (missing 'DataLakeStorage' connection string).",
                cancellationToken);
        }

        var scene = await _dbContext.FilmScenes
            .AsNoTracking()
            .Where(s => s.FilmSceneId == sceneId)
            .Select(s => new { s.FinalImageStoragePath })
            .FirstOrDefaultAsync(cancellationToken);

        if (scene?.FinalImageStoragePath is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SceneImageNotFound",
                $"No finished image found for FilmScene {sceneId}.", cancellationToken);
        }

        var content = await _storage.DownloadIllustrationBytesAsync(scene.FinalImageStoragePath, cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "image/png");
        response.Headers.Add("Cache-Control", "public, max-age=31536000, immutable");
        await response.Body.WriteAsync(content.ToMemory(), cancellationToken);
        return response;
    }
}
