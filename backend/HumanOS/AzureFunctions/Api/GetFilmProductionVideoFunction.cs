using System.Net;
using HumanOS.Data;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Streams the merged final film video for a FilmProduction
/// (2026-08-24, Fase 2) — same "resolve reference in SQL, then stream blob"
/// split as GetFilmSceneVideoFunction.</summary>
public sealed class GetFilmProductionVideoFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CapabilityGraphIllustrationStorageService _storage;

    public GetFilmProductionVideoFunction(HumanOsDbContext dbContext, CapabilityGraphIllustrationStorageService storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    [Function("GetFilmProductionVideo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/film-productions/{filmProductionId:guid}/video")]
        HttpRequestData request,
        Guid filmProductionId,
        CancellationToken cancellationToken)
    {
        if (!_storage.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StorageNotConfigured",
                "CapabilityGraphIllustrationStorageService is not configured (missing 'DataLakeStorage' connection string).",
                cancellationToken);
        }

        var production = await _dbContext.FilmProductions
            .AsNoTracking()
            .Where(p => p.FilmProductionId == filmProductionId)
            .Select(p => new { p.FinalFilmStoragePath })
            .FirstOrDefaultAsync(cancellationToken);

        if (production?.FinalFilmStoragePath is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "FilmVideoNotFound",
                $"No merged film found for FilmProduction {filmProductionId}.", cancellationToken);
        }

        var content = await _storage.DownloadIllustrationBytesAsync(production.FinalFilmStoragePath, cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "video/mp4");
        response.Headers.Add("Cache-Control", "public, max-age=31536000, immutable");
        await response.Body.WriteAsync(content.ToMemory(), cancellationToken);
        return response;
    }
}
