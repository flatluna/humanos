using System.Net;
using System.Text.Json;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class CreateFilmSceneRequest
{
    public Guid TenantId { get; set; }
    public int SortOrder { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? DialogueText { get; set; }
    public string? Characters { get; set; }
    public string? CameraMovements { get; set; }
    public string Environment { get; set; } = string.Empty;

    /// <summary>"Uploaded", "GeneratedFromScenePrompt", or "GeneratedFromCustomPrompt".</summary>
    public string AnchorImageMode { get; set; } = "GeneratedFromScenePrompt";

    public string? CustomImagePrompt { get; set; }
    public string? AnchorImageBase64 { get; set; }
}

/// <summary>
/// Runs the Film Production two-agent pipeline for ONE scene (2026-08-24):
/// reviews the user's raw notes into a polished prompt + name, then
/// generates/composites the final image — see
/// <see cref="FilmProductionOrchestrator"/>. Synchronous (no polling) —
/// image generation for one scene is a single request/response, unlike
/// the longer-running video pipeline.
/// </summary>
public sealed class CreateFilmSceneFunction
{
    private readonly FilmProductionOrchestrator _orchestrator;

    public CreateFilmSceneFunction(FilmProductionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("CreateFilmScene")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/film-productions/{filmProductionId:guid}/scenes")]
        HttpRequestData request,
        Guid filmProductionId,
        CancellationToken cancellationToken)
    {
        if (!_orchestrator.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "FilmProductionNotConfigured",
                "Film Production is not configured (missing Azure OpenAI or Data Lake storage settings).", cancellationToken);
        }

        CreateFilmSceneRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CreateFilmSceneRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Description) || string.IsNullOrWhiteSpace(body.Environment))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "Description and Environment are required.", cancellationToken);
        }

        if (!Enum.TryParse<FilmSceneAnchorImageMode>(body.AnchorImageMode, ignoreCase: true, out var anchorMode))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidAnchorImageMode",
                "AnchorImageMode must be one of: Uploaded, GeneratedFromScenePrompt, GeneratedFromCustomPrompt.", cancellationToken);
        }

        byte[]? anchorImageBytes = null;
        if (!string.IsNullOrWhiteSpace(body.AnchorImageBase64))
        {
            try
            {
                anchorImageBytes = Convert.FromBase64String(body.AnchorImageBase64);
            }
            catch (FormatException)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "InvalidImage", "AnchorImageBase64 is not valid base64.", cancellationToken);
            }
        }

        try
        {
            var scene = await _orchestrator.ProcessSceneAsync(new FilmProductionOrchestrator.ProcessSceneRequest
            {
                TenantId = body.TenantId,
                FilmProductionId = filmProductionId,
                SortOrder = body.SortOrder,
                Description = body.Description,
                DialogueText = body.DialogueText,
                Characters = body.Characters,
                CameraMovements = body.CameraMovements,
                Environment = body.Environment,
                AnchorImageMode = anchorMode,
                CustomImagePrompt = body.CustomImagePrompt,
                AnchorImageBytes = anchorImageBytes,
            }, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(request, scene, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidSceneRequest", ex.Message, cancellationToken);
        }
    }
}
