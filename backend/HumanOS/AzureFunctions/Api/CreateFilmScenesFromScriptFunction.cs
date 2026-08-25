using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class CreateFilmScenesFromScriptRequest
{
    public Guid TenantId { get; set; }
    public string FullScriptText { get; set; } = string.Empty;

    /// <summary>Optional: the film's locations (e.g. "Classroom: bright
    /// modern classroom with whiteboard...") so the breakdown agent reuses
    /// the SAME environment description every time a scene returns to that
    /// location, instead of inventing a slightly different one each time.</summary>
    public string? EnvironmentsDescription { get; set; }

    /// <summary>Optional cap on how many scenes the breakdown agent may
    /// produce (default 8, clamped 1-20 server-side) — keeps a dense script
    /// from exploding into dozens of near-duplicate scenes.</summary>
    public int? MaxScenes { get; set; }

    /// <summary>Optional base64 photo (e.g. the teacher/host) used as the
    /// first scene's reference image — every subsequent scene then chains
    /// from the PREVIOUS scene's own final image, so the whole storyboard
    /// stays visually consistent instead of each scene looking unrelated.
    /// If omitted, the first scene is generated from scratch (unchanged
    /// prior behavior) and everything after it still chains from there.</summary>
    public string? InitialAnchorImageBase64 { get; set; }
}

/// <summary>
/// "Auto-storyboard" mode (2026-08-24): given a full script/story text,
/// breaks it into an ordered sequence of 10-second scenes and runs the
/// full two-agent pipeline for every one of them, returning the complete
/// list already generated. Alternative to the manual, one-scene-at-a-time
/// wizard loop (<see cref="CreateFilmSceneFunction"/>) — see
/// <see cref="FilmProductionOrchestrator.ProcessScriptAsync"/>.
///
/// Synchronous by design like the manual endpoint, but can legitimately
/// take minutes for a long script (one sequential image generation per
/// scene) — callers should set a generous client-side timeout.
/// </summary>
public sealed class CreateFilmScenesFromScriptFunction
{
    private readonly FilmProductionOrchestrator _orchestrator;

    public CreateFilmScenesFromScriptFunction(FilmProductionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("CreateFilmScenesFromScript")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/film-productions/{filmProductionId:guid}/scenes/from-script")]
        HttpRequestData request,
        Guid filmProductionId,
        CancellationToken cancellationToken)
    {
        if (!_orchestrator.IsScriptModeConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "FilmProductionNotConfigured",
                "Film Production script mode is not configured (missing Azure OpenAI or Data Lake storage settings).", cancellationToken);
        }

        CreateFilmScenesFromScriptRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CreateFilmScenesFromScriptRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.FullScriptText))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "FullScriptText is required.", cancellationToken);
        }

        try
        {
            byte[]? initialAnchorImageBytes = null;
            if (!string.IsNullOrWhiteSpace(body.InitialAnchorImageBase64))
            {
                try
                {
                    initialAnchorImageBytes = Convert.FromBase64String(body.InitialAnchorImageBase64);
                }
                catch (FormatException)
                {
                    return await FunctionResponseFactory.ErrorResponseAsync(
                        request, HttpStatusCode.BadRequest, "InvalidImage", "InitialAnchorImageBase64 is not valid base64.", cancellationToken);
                }
            }

            var scenes = await _orchestrator.ProcessScriptAsync(
                body.TenantId, filmProductionId, body.FullScriptText, body.EnvironmentsDescription, body.MaxScenes, initialAnchorImageBytes, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, scenes, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidScriptRequest", ex.Message, cancellationToken);
        }
    }
}
