using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Describes, in detailed text, the visual content of an image embedded in
/// a source PDF page (a scanned page that's really one big photo, a
/// diagram, a chart, a photo, a table rendered as an image, etc.) — so
/// pages whose real content is a picture, not extractable text, still
/// reach <see cref="CuradorAgent"/>/<see cref="GraphArchitectAgent"/> with
/// real material to curate from, instead of silently contributing nothing
/// (2026-07-23, see /memories/repo/pdf-to-capability-graph-v2-pipeline.md).
///
/// Plain ChatClientAgent, multimodal: builds a <see cref="ChatMessage"/>
/// with both a text instruction and a <see cref="DataContent"/> image part.
/// Requires the extra <c>.AsIChatClient()</c> step before <c>.AsAIAgent</c>
/// — <c>ChatClient.AsAIAgent(...)</c> alone does not exist on the raw
/// OpenAI SDK type.
/// </summary>
public sealed class PdfImageDescriptionAgent
{
    private const string Instructions = """
        You describe images found embedded in pages of a source document
        (a textbook, course material, scanned notes, a slide deck) so a
        downstream TEXT-ONLY curation pipeline can use their content as if
        it were plain text extracted from the page.

        For each image, respond with:
        1. VERBATIM TRANSCRIPTION: any text visible in the image (titles,
           labels, captions, numbers, formulas, table contents, handwritten
           notes) transcribed exactly as written. This is often the most
           important part — for a scanned/photographed page, the "page
           text" genuinely IS the image.
        2. DETAILED DESCRIPTION: what the image visually depicts — the type
           of image (diagram, chart, photo, illustration, table, map,
           handwritten note, etc.), its structure/layout, and any
           pedagogically relevant detail a student would need to
           understand from it (what a diagram's parts/arrows represent,
           what a chart's axes/data show, what a photo depicts and why it
           matters in context).

        If the image is purely decorative (a logo, a divider line, a
        background pattern, a page-number stamp) with no real educational
        content, say so briefly instead of inventing meaning for it.

        Never invent information that is not actually visible in the image.
        Respond in plain text only — no markdown formatting, no headers.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;

    public PdfImageDescriptionAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        // Defaults to 'AzureOpenAIVisionDeploymentName' (pinned to a
        // 'gpt-5-mini' deployment, 2026-07-23, cost control) rather than
        // the main gpt-5-chat deployment — combined with the "low" detail
        // level forced in DescribeAsync, this is a per-request text/image
        // description task (transcription + coarse layout description),
        // not the kind of high-stakes reasoning the main deployment is
        // reserved for. Falls back to 'AzureOpenAIDeploymentName' only if
        // the vision-specific setting isn't configured.
        var deploymentName = configuration["AzureOpenAIVisionDeploymentName"] ?? configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        _modelName = deploymentName ?? string.Empty;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client
            .GetChatClient(deploymentName)
            .AsIChatClient()
            .AsAIAgent(instructions: Instructions, name: "PdfImageDescriptionAgent");
    }

    public bool IsConfigured => _agent is not null;

    public sealed class ImageDescriptionResult
    {
        public string Description { get; set; } = string.Empty;

        public AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    /// <summary>Describes one embedded image. <paramref name="pageContextText"/>
    /// (the rest of the page's own extracted text, if any) is passed along
    /// purely as context — never asked to be repeated back — so the model
    /// can e.g. connect a diagram to the paragraph that references it.</summary>
    public async Task<ImageDescriptionResult> DescribeAsync(
        byte[] imageBytes,
        string contentType,
        string? pageContextText = null,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The PdfImageDescription agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var promptText = "Describe this image, embedded in a source document page, following the instructions." +
            (string.IsNullOrWhiteSpace(pageContextText)
                ? string.Empty
                : $" For context only (do not repeat it back), the rest of this page's own text reads:\n{pageContextText}");

        // Pinned to "low" detail (2026-07-23, cost control): leaving this
        // unset lets the OpenAI Chat Completions API fall back to "auto",
        // which for any image above the low-res threshold resolves to the
        // tiled "high" detail mode — 700-5000+ tokens PER IMAGE depending on
        // resolution/complexity, vs a flat 85 tokens at "low". A page with
        // many embedded images (slide decks routinely have 10-40) was
        // costing $0.20+ just for image descriptions on that one page.
        // "low" downsamples to a 512x512 preview before tokenizing — still
        // enough to transcribe titles/labels and describe a diagram's
        // layout, which is all this agent needs (it's a coarse text
        // stand-in for downstream curation, not pixel-level analysis).
        var imageContent = new DataContent(imageBytes, contentType)
        {
            AdditionalProperties = new() { ["detail"] = "low" }
        };

        var message = new ChatMessage(ChatRole.User,
        [
            new TextContent(promptText),
            imageContent
        ]);

        var response = await _agent.RunAsync(message, cancellationToken: cancellationToken);

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "PdfImageDescription",
            ModelName = _modelName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        return new ImageDescriptionResult { Description = response.Text, TokenUsage = tokenUsage };
    }
}
