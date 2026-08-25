using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Deletes one FilmScene — removes both the DB row and its
/// anchor/final image blobs (best-effort; a blob that's already gone must
/// never block deleting the row).</summary>
public sealed class DeleteFilmSceneFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _storage;

    public DeleteFilmSceneFunction(HumanOsDbContext dbContext, CapabilityGraphIllustrationStorageService storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    [Function("DeleteFilmScene")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "studio/film-productions/scenes/{sceneId:guid}")]
        HttpRequestData request,
        Guid sceneId,
        CancellationToken cancellationToken)
    {
        var scene = await _dbContext.FilmScenes.FirstOrDefaultAsync(s => s.FilmSceneId == sceneId, cancellationToken);

        if (scene is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SceneNotFound", $"No FilmScene found with id {sceneId}.", cancellationToken);
        }

        foreach (var path in new[] { scene.AnchorImageStoragePath, scene.FinalImageStoragePath })
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            try
            {
                await _storage.DeleteIllustrationAsync(path, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort — never block deleting the row over a storage hiccup.
            }
        }

        _dbContext.FilmScenes.Remove(scene);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { deleted = true }, cancellationToken: cancellationToken);
    }
}
