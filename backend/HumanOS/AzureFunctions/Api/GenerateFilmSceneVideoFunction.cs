using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GenerateFilmSceneVideoRequest
{
    /// <summary>Optional (2026-08-24): overrides the stored GeneratedPrompt
    /// for this ONE video call \u2014 lets a caller retry with different
    /// wording (e.g. after Omni Flash rejects a real person's name/likeness
    /// in the prompt) without regenerating the scene's image/content.</summary>
    public string? Prompt { get; set; }
}

/// <summary>Generates (or regenerates) the review/edit video for ONE
/// FilmScene, image-to-video from its FinalImageStoragePath + GeneratedPrompt
/// (2026-08-24, Fase 2). Synchronous — Omni Flash typically completes in
/// well under a minute per Google's docs.</summary>
public sealed class GenerateFilmSceneVideoFunction
{
    private readonly FilmProductionVideoOrchestrator _orchestrator;

    public GenerateFilmSceneVideoFunction(FilmProductionVideoOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GenerateFilmSceneVideo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/film-productions/scenes/{sceneId:guid}/video")]
        HttpRequestData request,
        Guid sceneId,
        CancellationToken cancellationToken)
    {
        if (!_orchestrator.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "FilmProductionVideoNotConfigured",
                "Film Production video generation is not configured (missing GoogleVeoApiKey or Data Lake storage settings).", cancellationToken);
        }

        GenerateFilmSceneVideoRequest? body = null;
        if (request.Body.Length > 0)
        {
            try
            {
                body = await JsonSerializer.DeserializeAsync<GenerateFilmSceneVideoRequest>(
                    request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
            }
            catch (JsonException)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
            }
        }

        try
        {
            var scene = await _orchestrator.GenerateSceneVideoAsync(sceneId, body?.Prompt, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, scene, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidVideoRequest", ex.Message, cancellationToken);
        }
    }
}
