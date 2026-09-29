using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GenerateVideoProductionClipRequest
{
    public int SortOrder { get; set; }
    public string Prompt { get; set; } = string.Empty;

    /// <summary>"veo" (default, fast tier), "veo-lite" (cheapest Veo
    /// tier), "omni" (Gemini Omni Flash Preview), or "kling" (Kling AI).</summary>
    public string VideoModel { get; set; } = "veo";

    /// <summary>Optional starting frame — the user's chosen starting image
    /// for clip 0, or a previous clip's last frame for continuity.</summary>
    public string? ReferenceImageBase64 { get; set; }
    public string? ReferenceImageContentType { get; set; }
}

/// <summary>Generates ONE clip of a standalone Video Production directly
/// from a text prompt (2026-09-23) — no per-scene image-generation agent
/// step, unlike Film Productions. Synchronous: Veo can take a few minutes,
/// Omni Flash typically well under a minute.</summary>
public sealed class GenerateVideoProductionClipFunction
{
    private readonly VideoProductionOrchestrator _orchestrator;

    public GenerateVideoProductionClipFunction(VideoProductionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GenerateVideoProductionClip")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/video-productions/{videoProductionId:guid}/clips")]
        HttpRequestData request,
        Guid videoProductionId,
        CancellationToken cancellationToken)
    {
        if (!_orchestrator.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "VideoProductionNotConfigured",
                "Video Productions is not configured (missing GoogleVeoApiKey or Data Lake storage settings).", cancellationToken);
        }

        GenerateVideoProductionClipRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<GenerateVideoProductionClipRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Prompt))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "Prompt is required.", cancellationToken);
        }

        try
        {
            byte[]? referenceImageBytes = null;
            if (!string.IsNullOrWhiteSpace(body.ReferenceImageBase64))
            {
                referenceImageBytes = Convert.FromBase64String(body.ReferenceImageBase64);
            }

            var clip = await _orchestrator.GenerateClipAsync(
                videoProductionId, body.SortOrder, body.Prompt,
                body.VideoModel is "omni" or "kling" or "veo-lite" ? body.VideoModel : "veo",
                referenceImageBytes, body.ReferenceImageContentType, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, clip, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidClipRequest", ex.Message, cancellationToken);
        }
    }
}
