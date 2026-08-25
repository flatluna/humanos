using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Generates videos for every Completed scene in a production that
/// doesn't already have one, sequentially (2026-08-24, Fase 2) — the
/// wizard's "Generate all videos" step. Synchronous by design like
/// CreateFilmScenesFromScriptFunction; can take minutes for many scenes.</summary>
public sealed class GenerateFilmProductionVideosFunction
{
    private readonly FilmProductionVideoOrchestrator _orchestrator;

    public GenerateFilmProductionVideosFunction(FilmProductionVideoOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GenerateFilmProductionVideos")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/film-productions/{filmProductionId:guid}/videos")]
        HttpRequestData request,
        Guid filmProductionId,
        CancellationToken cancellationToken)
    {
        if (!_orchestrator.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "FilmProductionVideoNotConfigured",
                "Film Production video generation is not configured (missing GoogleVeoApiKey or Data Lake storage settings).", cancellationToken);
        }

        try
        {
            var scenes = await _orchestrator.GenerateAllPendingAsync(filmProductionId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, scenes, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidVideoRequest", ex.Message, cancellationToken);
        }
    }
}
