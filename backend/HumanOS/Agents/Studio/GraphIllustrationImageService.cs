using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Common;
using Microsoft.Extensions.Configuration;
using OpenAI.Images;
using System.ClientModel;

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
        // 'AzureOpenAIImageEndpoint'/'AzureOpenAIImageApiKey' (2026-08-06, dual-resource
        // migration): gpt-image-2 stayed pinned on twinet-resource (its RPM quota is
        // maxed out subscription-wide, see /memories/repo migration notes) while every
        // other deployment moved to engramsediucation-resource — these let THIS one
        // service point at a different account than 'AzureOpenAIEndpoint'/'AzureOpenAIApiKey'.
        // Falls back to the shared settings when not set, so nothing breaks for anyone
        // still on a single-resource setup.
        var endpoint = configuration["AzureOpenAIImageEndpoint"] ?? configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIImageDeploymentName"];
        var apiKey = configuration["AzureOpenAIImageApiKey"] ?? configuration["AzureOpenAIApiKey"];

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
    /// Image calls run from background pipelines (course creation, node
    /// addition), never a live interactive request — safe to wait much
    /// longer than QuotaRetryPolicy's default ~60s budget for gpt-image-2's
    /// occasional longer/hourly-style throttling (2026-08-06), while still
    /// staying well under the Function host's 9m30s functionTimeout even
    /// when several nodes retry concurrently.
    /// </summary>
    private static readonly TimeSpan ImageQuotaMaxTotalWait = TimeSpan.FromMinutes(4);

    private static readonly TimeSpan[] ImageQuotaBackoffDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(90),
    ];

    /// <summary>
    /// Reads the server's own Retry-After delay off a 429 response when the
    /// SDK exposes it, instead of always falling back to a generic backoff —
    /// honoring the real value matters most for gpt-image-2's longer/hourly
    /// quota windows, where guessing too short just burns through retries
    /// for nothing.
    /// </summary>
    private static TimeSpan? TryGetRetryAfter(Exception ex)
    {
        if (ex is not ClientResultException { Status: 429 } clientEx)
            return null;

        try
        {
            var headers = clientEx.GetRawResponse()?.Headers;
            if (headers is not null && headers.TryGetValue("Retry-After", out var raw)
                && int.TryParse(raw, out var seconds) && seconds > 0)
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }
        catch
        {
            // Best-effort header parsing — fall through to the generic backoff.
        }

        return TimeSpan.Zero;
    }

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

    /// <summary>
    /// Guidance appended when a PROFESSOR-uploaded reference image is
    /// supplied for one specific node (Capability Studio "Agregar nodo"
    /// modal, 2026-08-06 "usar mi imagen" feature) — distinct from
    /// <see cref="LogoReferenceInstruction"/> (a company brand logo reused
    /// across every illustration in a capability). Tells the model to
    /// actually use the reference image's own content, honoring whatever
    /// the professor wrote in their own prompt about how to use it.
    /// </summary>
    private const string UserReferenceInstruction =
        "The professor also attached a reference image for this specific illustration. Use its visual " +
        "content as guidance, following any instructions about it given in the prompt above (e.g. keep " +
        "the same subject/style, or take just one element from it). Adapt it as needed to fit this " +
        "illustration rather than reproducing it unchanged, unless told otherwise.";

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
            return await EditWithReferenceAsync(prompt, logoImageBytes, "brand-logo.png", LogoReferenceInstruction, cancellationToken);
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

        // Quota-retry (2026-08-06): see the image-edit branch above for why.
        var response = await QuotaRetryPolicy.ExecuteAsync(
            () => _client.GenerateImageAsync(prompt, options, cancellationToken),
            classify: TryGetRetryAfter,
            cancellationToken,
            maxTotalWait: ImageQuotaMaxTotalWait,
            backoffDelays: ImageQuotaBackoffDelays);
        var image = response.Value;

        return new GeneratedIllustration
        {
            ImageBytes = image.ImageBytes,
            ImageModel = _modelName,
            Width = 1024,
            Height = 1024
        };
    }

    /// <summary>
    /// Generates one illustration using a PROFESSOR-uploaded reference image
    /// (Capability Studio "Agregar nodo" modal option: "generar con IA
    /// usando mi imagen") instead of the capability's brand logo — same
    /// image-edit mechanism as <see cref="GenerateAsync"/>'s logo path, but
    /// scoped to a single node's illustration and phrased around honoring
    /// the professor's own prompt instructions about the reference image.
    /// </summary>
    public async Task<GeneratedIllustration> GenerateFromReferenceAsync(
        string prompt,
        byte[] referenceImageBytes,
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

        if (referenceImageBytes is not { Length: > 0 })
            throw new ArgumentException("referenceImageBytes is required", nameof(referenceImageBytes));

#pragma warning disable OPENAI001 // GeneratedImageQuality.LowQuality is an experimental SDK member (2.9.1) — explicitly opted into for cost control.
        return await EditWithReferenceAsync(prompt, referenceImageBytes, "reference.png", UserReferenceInstruction, cancellationToken);
#pragma warning restore OPENAI001
    }

#pragma warning disable OPENAI001 // GeneratedImageQuality.LowQuality is an experimental SDK member (2.9.1) — explicitly opted into for cost control.
    private async Task<GeneratedIllustration> EditWithReferenceAsync(
        string prompt,
        byte[] referenceImageBytes,
        string referenceFileName,
        string instructionSuffix,
        CancellationToken cancellationToken)
    {
        var editOptions = new ImageEditOptions
        {
            Size = GeneratedImageSize.W1024xH1024,
            Quality = GeneratedImageQuality.LowQuality,
        };

        using var referenceStream = new MemoryStream(referenceImageBytes);
        // Quota-retry (2026-08-06): gpt-image-2 on twinet-resource is pinned right at
        // its subscription-wide RPM quota ceiling, so a transient 429 here is expected
        // occasionally under load — wait it out instead of failing the whole request.
        var editResponse = await QuotaRetryPolicy.ExecuteAsync(
            () => _client!.GenerateImageEditAsync(
                referenceStream, referenceFileName, prompt + "\n\n" + instructionSuffix, editOptions, cancellationToken),
            classify: TryGetRetryAfter,
            cancellationToken,
            maxTotalWait: ImageQuotaMaxTotalWait,
            backoffDelays: ImageQuotaBackoffDelays);
        var editedImage = editResponse.Value;

        return new GeneratedIllustration
        {
            ImageBytes = editedImage.ImageBytes,
            ImageModel = _modelName,
            Width = 1024,
            Height = 1024
        };
    }
#pragma warning restore OPENAI001
}
