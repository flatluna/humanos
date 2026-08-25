using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Streams the generated review/edit video bytes for one FilmScene
/// (2026-08-24, Fase 2) — same "resolve reference in SQL, then stream blob"
/// split as GetFilmSceneImageFunction.</summary>
public sealed class GetFilmSceneVideoFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _storage;

    public GetFilmSceneVideoFunction(HumanOsDbContext dbContext, CapabilityGraphIllustrationStorageService storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    [Function("GetFilmSceneVideo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/film-productions/scenes/{sceneId:guid}/video")]
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
            .Select(s => new { s.FinalVideoStoragePath })
            .FirstOrDefaultAsync(cancellationToken);

        if (scene?.FinalVideoStoragePath is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SceneVideoNotFound",
                $"No finished video found for FilmScene {sceneId}.", cancellationToken);
        }

        var content = await _storage.DownloadIllustrationBytesAsync(scene.FinalVideoStoragePath, cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "video/mp4");
        response.Headers.Add("Cache-Control", "public, max-age=31536000, immutable");
        await response.Body.WriteAsync(content.ToMemory(), cancellationToken);
        return response;
    }
}
