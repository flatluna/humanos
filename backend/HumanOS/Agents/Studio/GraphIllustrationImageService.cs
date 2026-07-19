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
    /// deployment.
    /// </summary>
    public async Task<GeneratedIllustration> GenerateAsync(
        string prompt,
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

        // gpt-image-1.5 (unlike dall-e-3) does not accept a "response_format"
        // parameter — it always returns base64-encoded image bytes, so
        // ResponseFormat/GeneratedImageFormat must NOT be set here or the
        // API rejects the request with "unknown_parameter: response_format".
        var options = new ImageGenerationOptions
        {
            Size = GeneratedImageSize.W1024xH1024,
        };

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
