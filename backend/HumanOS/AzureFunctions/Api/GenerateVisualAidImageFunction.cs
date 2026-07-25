using System.Net;
using System.Text.Json;
using HumanOS.Agents.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Generates a single, on-demand image (gpt-image-1.5) for the Realtime
/// "show_visual_aid" tool's kind="generated_image" (2026-07-24) — used when
/// a student asks the Agent (Reactivar Engrama / Voice Tutor) to actually
/// DRAW/show a picture of something that isn't one of the lesson's
/// already-generated illustrations (e.g. "muéstrame una imagen de dos
/// personas diciendo good morning"). Deliberately ephemeral: returns the
/// image as base64 only, never persisted to the database or Data Lake
/// (same pattern as GenerateProgramLogoFunction) — it's a improvised,
/// in-the-moment visual aid for one conversation, not real course material.
/// </summary>
public sealed class GenerateVisualAidImageFunction
{
    private readonly GraphIllustrationImageService _imageService;

    public GenerateVisualAidImageFunction(GraphIllustrationImageService imageService)
    {
        _imageService = imageService;
    }

    public sealed class GenerateRequest
    {
        public string Prompt { get; set; } = string.Empty;
    }

    public sealed class GenerateResponse
    {
        public string ImageBase64 { get; set; } = string.Empty;

        public string ContentType { get; set; } = "image/png";
    }

    [Function("GenerateVisualAidImage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/tutor/visual-aid/generate-image")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_imageService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "ImageGenerationNotConfigured",
                "Image generation is not configured (missing Azure OpenAI image deployment settings).",
                cancellationToken);
        }

        GenerateRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<GenerateRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Prompt))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest", "'prompt' is required.", cancellationToken);
        }

        // Capped so a runaway/adversarial prompt from the model can't blow
        // up cost or request size — plenty for a rich descriptive prompt.
        var prompt = body.Prompt.Length > 800 ? body.Prompt[..800] : body.Prompt;

        try
        {
            var generated = await _imageService.GenerateAsync(prompt, cancellationToken);

            var response = new GenerateResponse
            {
                ImageBase64 = Convert.ToBase64String(generated.ImageBytes.ToArray()),
                ContentType = "image/png",
            };

            return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            // Never let an image-generation failure (rate limit, content
            // policy rejection, transient Azure error) crash the voice
            // session — the frontend answers the tool call with a graceful
            // "couldn't draw it" result so the Agent can say so out loud
            // and move on, instead of the turn stalling forever.
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadGateway, "ImageGenerationFailed", ex.Message, cancellationToken);
        }
    }
}
