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
        (a textbook, course material, scanned notes, a slide deck, an
        infographic/slide) so a downstream curation pipeline can use their
        content as real course material — not just a vague caption.

        Respond in Markdown, structured as:

        # Título
        The image's own title/heading if one is visible (e.g. a slide
        title, a diagram's name). If no explicit title is visible, infer a
        short, accurate one from the content — never leave this generic
        ("Imagen 1", "Diagrama").

        # Resumen Ejecutivo
        A short paragraph (3-6 sentences) summarizing what the image is
        about and why it matters, as if introducing it to a student.

        # Índice de Contenidos
        Transcribe the image's real content as nested Markdown headings and
        bullet lists that mirror its actual structure (sections, numbered
        parts, categories, steps). Use real headings (##, ###) for each
        section/box/column you can identify, and bullet lists for the
        items inside each one. Transcribe all legible text verbatim under
        its matching heading — titles, labels, captions, numbers, table
        contents, handwritten notes. Read every part of the image
        carefully, including small text in boxes/columns/legends, before
        concluding something is illegible; only state text is unreadable
        if it genuinely cannot be made out at all.

        If the image is purely decorative (a logo, a divider line, a
        background pattern, a page-number stamp) with no real educational
        content, say so briefly instead of inventing structure for it.

        Never invent information that is not actually visible in the image.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;

    public PdfImageDescriptionAgent(IConfiguration configuration)
    {
        // Prefers the stronger 'Gpt61Sol*' deployment (2026-11-05): manual
        // testing showed the previous cheap 'gpt-5-mini' + "low" detail
        // combo produced vague, illegible-text excuses on dense
        // infographics/slides, while this deployment (same family the user
        // validated directly) reads small text and structure far better.
        // Falls back to 'AzureOpenAIVisionDeploymentName'/
        // 'AzureOpenAIDeploymentName' if the stronger deployment isn't
        // configured.
        var endpoint = configuration["Gpt61SolEndpoint"] ?? configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["Gpt61SolDeploymentName"]
            ?? configuration["AzureOpenAIVisionDeploymentName"]
            ?? configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["Gpt61SolApiKey"] ?? configuration["AzureOpenAIApiKey"];

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

        // Raised to "high" detail (2026-11-05): "low" downsampled every
        // image to a 512x512 preview before tokenizing, which made dense
        // infographics/slides with small text genuinely illegible to the
        // model — it would (correctly, given what it was shown) report
        // text as "too small to read". "high" uses the tiled encoding so
        // small labels/body text in slide-style images stay legible. Costs
        // more per image (roughly 700-1500+ tokens vs a flat 85 at "low"),
        // but is required for this agent to produce real transcriptions
        // instead of vague guesses.
        var imageContent = new DataContent(imageBytes, contentType)
        {
            AdditionalProperties = new() { ["detail"] = "high" }
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
