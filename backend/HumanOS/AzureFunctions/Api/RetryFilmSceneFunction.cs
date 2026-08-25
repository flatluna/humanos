using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Re-runs Agent A + Agent B for one EXISTING scene (2026-08-24) —
/// e.g. after an agent-instruction fix, so a scene previously blocked by
/// image moderation (or otherwise Failed) can be retried without
/// recreating it or losing its place in the storyboard.</summary>
public sealed class RetryFilmSceneFunction
{
    private readonly FilmProductionOrchestrator _orchestrator;

    public RetryFilmSceneFunction(FilmProductionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("RetryFilmScene")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/film-productions/scenes/{sceneId:guid}/retry")]
        HttpRequestData request,
        Guid sceneId,
        CancellationToken cancellationToken)
    {
        if (!_orchestrator.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "FilmProductionNotConfigured",
                "Film Production is not configured (missing Azure OpenAI or Data Lake storage settings).", cancellationToken);
        }

        try
        {
            var scene = await _orchestrator.RetrySceneAsync(sceneId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, scene, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRetryRequest", ex.Message, cancellationToken);
        }
    }
}
