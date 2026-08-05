using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using OpenAI.Images;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Wraps the Azure OpenAI image-generation deployment (gpt-image-1.5) used
/// to turn a CapabilityGraphNode's <c>IllustrationPrompts</c> (produced by
/// GraphArchitectAgent) into actual PNG image bytes. Configured via the same
/// flat 'AzureOpenAIEndpoint'/'AzureOpenAIApiKey' settings as the other
/// Studio agents, plus 'AzureOpenAIImageDeploymentName'.
///
/// Deliberately separate from GraphArchitectAgent (which only produces text
/// prompts) — image generation + Data Lake upload + DB metadata persistence
/// is orchestration work reserved for the executor that wires GraphArchitect
/// output into the database (Paso 3), matching the plain-agent, no-side-effects
/// pattern GraphArchitectAgent already follows.
///
/// Does NOT create a new Azure AI Foundry deployment — reuses the existing
/// resource/deployment named by 'AzureOpenAIImageDeploymentName'.
/// </summary>
public sealed class GraphIllustrationImageService
{
    private readonly ImageClient? _client;
    private readonly string _modelName;

    public GraphIllustrationImageService(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIImageDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        _modelName = deploymentName ?? string.Empty;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _client = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _client = client.GetImageClient(deploymentName);
    }

    public bool IsConfigured => _client is not null;

    /// <summary>
    /// Guidance appended to the prompt whenever a brand-logo reference image
    /// is supplied (2026-08-04 "company logo in illustrations" feature) —
    /// deliberately leaves the EXACT placement to the model's own judgment
    /// (clothing, a framed picture/sign, a storefront window, or simply
    /// visible somewhere in the composition) rather than forcing one fixed
    /// spot, since the right placement varies scene by scene. Also tells
    /// the model to keep the logo faithful to the reference instead of
    /// redesigning it.
    /// </summary>
    private const string LogoReferenceInstruction =
        "Also use the attached brand logo image as a reference somewhere in this scene, " +
        "wherever it's a genuinely good idea. You have full creative discretion to decide the " +
        "most natural placement and context — for example on people's clothing (t-shirts, " +
        "uniforms), a framed picture or sign on a wall, a shop/storefront window, or simply " +
        "displayed somewhere visible so the brand is recognizable — whatever fits this specific " +
        "scene best. Keep the logo's shape, colors, and proportions faithful to the reference " +
        "image; do not redesign or reinterpret it.";

    public sealed class GeneratedIllustration
    {
        public BinaryData ImageBytes { get; set; } = null!;

        public string ImageModel { get; set; } = string.Empty;

        public int Width { get; set; }

        public int Height { get; set; }
    }

    /// <summary>
    /// Generates one PNG illustration from a text prompt (typically one of
    /// GraphNodeDto.IllustrationPrompts) using the configured gpt-image-1.5
    /// deployment. When <paramref name="logoImageBytes"/> is provided (a
    /// company's real brand logo, must be a valid PNG), uses the image-edit
    /// endpoint instead of plain text-to-image so the model actually SEES
    /// the real logo as a reference and can weave it into the generated
    /// scene (see <see cref="LogoReferenceInstruction"/>) — plain
    /// text-to-image can only ever guess/hallucinate a logo from a
    /// description, never reproduce a real one.
    /// </summary>
    public async Task<GeneratedIllustration> GenerateAsync(
        string prompt,
        byte[]? logoImageBytes = null,
        CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            throw new InvalidOperationException(
                "The illustration image service is not configured. Set the 'AzureOpenAIEndpoint' " +
                "and 'AzureOpenAIImageDeploymentName' application settings.");
        }

        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt is required", nameof(prompt));

        // Quality explicitly pinned to Low (2026-07-19, user cost-control
        // decision): leaving Quality unset defaults to "auto" (the model
        // picks, usually resolving to a more expensive medium/high tier),
        // which made per-run image cost unpredictable. Low is the cheapest
        // gpt-image-1.5 tier and is enough for the Hypothesis/Teaching
        // illustration prompts' simple diagrams.
#pragma warning disable OPENAI001 // GeneratedImageQuality.LowQuality is an experimental SDK member (2.9.1) — explicitly opted into for cost control.
        if (logoImageBytes is { Length: > 0 })
        {
            var editOptions = new ImageEditOptions
            {
                Size = GeneratedImageSize.W1024xH1024,
                Quality = GeneratedImageQuality.LowQuality,
            };

            using var logoStream = new MemoryStream(logoImageBytes);
            var editResponse = await _client.GenerateImageEditAsync(
                logoStream, "brand-logo.png", $"{prompt}\n\n{LogoReferenceInstruction}", editOptions, cancellationToken);
            var editedImage = editResponse.Value;

            return new GeneratedIllustration
            {
                ImageBytes = editedImage.ImageBytes,
                ImageModel = _modelName,
                Width = 1024,
                Height = 1024
            };
        }

        // gpt-image-1.5 (unlike dall-e-3) does not accept a "response_format"
        // parameter on plain generation — it always returns base64-encoded
        // image bytes, so ResponseFormat/GeneratedImageFormat must NOT be
        // set here or the API rejects the request with
        // "unknown_parameter: response_format".
        var options = new ImageGenerationOptions
        {
            Size = GeneratedImageSize.W1024xH1024,
            Quality = GeneratedImageQuality.LowQuality,
        };
#pragma warning restore OPENAI001

        var response = await _client.GenerateImageAsync(prompt, options, cancellationToken);
        var image = response.Value;

        return new GeneratedIllustration
        {
            ImageBytes = image.ImageBytes,
            ImageModel = _modelName,
            Width = 1024,
            Height = 1024
        };
    }
}
