using System.Net;
using System.Text.Json;
using HumanOS.Agents.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Generates a logo image (gpt-image-1.5) for a Program from its name and
/// description — used by NewProgramPage's "Generar logo con IA" button.
/// Deliberately NOT tied to an existing ProgramId (called during the
/// creation wizard, before the Program row exists) and does NOT touch the
/// database or Data Lake — it only returns the generated image as base64
/// so the frontend can preview it and let the user Accept/Regenerate.
/// Actual persistence (Data Lake upload + Program.LogoStoragePath) happens
/// afterward through the existing UploadProgramLogoFunction, once the user
/// accepts the image and the Program has been created.
/// </summary>
public sealed class GenerateProgramLogoFunction
{
    private readonly GraphIllustrationImageService _imageService;

    public GenerateProgramLogoFunction(GraphIllustrationImageService imageService)
    {
        _imageService = imageService;
    }

    public sealed class GenerateRequest
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

    public sealed class GenerateResponse
    {
        public string ImageBase64 { get; set; } = string.Empty;

        public string ContentType { get; set; } = "image/png";
    }

    [Function("GenerateProgramLogo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programs/logo/generate")]
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

        var body = await JsonSerializer.DeserializeAsync<GenerateRequest>(
            request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        if (body is null || string.IsNullOrWhiteSpace(body.Name))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest",
                "'name' is required.", cancellationToken);
        }

        var prompt =
            $"A clean, modern, professional logo/emblem representing an educational program titled " +
            $"'{body.Name}'. {body.Description}. " +
            "Flat design, no text, no letters, no words, centered composition on a simple background, soft color palette, square format.";

        var generated = await _imageService.GenerateAsync(prompt, cancellationToken);

        var response = new GenerateResponse
        {
            ImageBase64 = Convert.ToBase64String(generated.ImageBytes.ToArray()),
            ContentType = "image/png",
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }
}
