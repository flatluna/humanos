using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Concatenates every scene's video (in SortOrder) into ONE final
/// film for the production (2026-08-24, Fase 2) — local ffmpeg merge, no
/// billed API call. Requires every completed scene to already have a
/// video.</summary>
public sealed class MergeFilmProductionVideosFunction
{
    private readonly FilmProductionVideoOrchestrator _orchestrator;

    public MergeFilmProductionVideosFunction(FilmProductionVideoOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("MergeFilmProductionVideos")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/film-productions/{filmProductionId:guid}/merge")]
        HttpRequestData request,
        Guid filmProductionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var production = await _orchestrator.MergeAllScenesAsync(filmProductionId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, production, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidMergeRequest", ex.Message, cancellationToken);
        }
    }
}
