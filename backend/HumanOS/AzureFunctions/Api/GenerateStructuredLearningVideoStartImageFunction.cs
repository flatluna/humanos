using System.Net;
using System.Text.Json;
using HumanOS.Agents.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GenerateStructuredLearningVideoStartImageRequest
{
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Optional uploaded reference image (2026-08-25) — when
    /// provided, the new image is generated FROM this one (image-edit)
    /// instead of from the text prompt alone, so an uploaded photo can be
    /// turned into a new starting-image candidate guided by it.</summary>
    public string? ReferenceImageBase64 { get; set; }
}

public sealed class GenerateStructuredLearningVideoStartImageResponse
{
    public string ImageBase64 { get; set; } = string.Empty;

    public string ContentType { get; set; } = "image/png";
}

/// <summary>
/// Generates a starting-image candidate for the video wizard's tab 3
/// (2026-08-25) — reuses GraphIllustrationImageService's cheapest tier
/// (gpt-image-1.5, LowQuality, same "image2" service other Studio features
/// already use) from the reviewer's own image description, so they don't
/// have to source/upload a photo themselves. Ephemeral like
/// GenerateVisualAidImageFunction: returns base64 only, never persisted —
/// the reviewer picks whether to keep/discard/regenerate before using it.
/// </summary>
public sealed class GenerateStructuredLearningVideoStartImageFunction
{
    private readonly GraphIllustrationImageService _imageService;

    public GenerateStructuredLearningVideoStartImageFunction(GraphIllustrationImageService imageService)
    {
        _imageService = imageService;
    }

    [Function("GenerateStructuredLearningVideoStartImage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/video-prompt/generate-start-image")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_imageService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "ImageGenerationNotConfigured",
                "Image generation is not configured (missing Azure OpenAI image deployment settings).", cancellationToken);
        }

        GenerateStructuredLearningVideoStartImageRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<GenerateStructuredLearningVideoStartImageRequest>(
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
            var generated = string.IsNullOrWhiteSpace(body.ReferenceImageBase64)
                ? await _imageService.GenerateAsync(body.Prompt, cancellationToken: cancellationToken)
                : await _imageService.GenerateFromReferenceAsync(
                    body.Prompt, Convert.FromBase64String(body.ReferenceImageBase64), cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(
                request,
                new GenerateStructuredLearningVideoStartImageResponse { ImageBase64 = Convert.ToBase64String(generated.ImageBytes.ToArray()) },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadGateway, "ImageGenerationFailed", ex.Message, cancellationToken);
        }
    }
}
