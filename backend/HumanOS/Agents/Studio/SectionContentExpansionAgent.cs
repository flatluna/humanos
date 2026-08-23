using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>Which configured Azure OpenAI deployment to use for a content
/// expansion call — the designer picks this explicitly in the wizard
/// rather than it being silently chosen for them.</summary>
public enum ContentExpansionModelChoice
{
    /// <summary>'AzureOpenAIDeploymentName' — the main, higher-quality deployment.</summary>
    Main,

    /// <summary>'AzureOpenAIEconomyDeploymentName' (falls back to Main if unset) — cheaper.</summary>
    Economy
}

public sealed class SectionContentExpansionResult
{
    public string ExpandedText { get; set; } = string.Empty;

    public AgentTokenUsage TokenUsage { get; set; } = null!;
}

/// <summary>
/// Opt-in "Generar contenido con IA" agent (2026-08-17) — for a section
/// whose own source text is a bare outline/index entry with no real
/// developed content (e.g. "Week 1: Alphabet Review, Letter Names, Letter
/// Sounds"), generates real explanatory prose from the LLM's own knowledge
/// (optionally supplemented with live web-search findings from
/// <see cref="WebGroundingService"/>) so Step 3's normally strictly
/// source-faithful <see cref="StructuredLearningSectionDesignerAgent"/> has
/// real content to work from instead of an almost-empty source.
///
/// Deliberately the OPPOSITE discipline from every other Studio extraction
/// agent (which never invent facts) — this one EXISTS to invent
/// pedagogically sound content for a topic label, which is exactly why it
/// is never automatic: the designer must explicitly opt in per document/
/// section, see RunFromDocumentTextAsync's own doc comment.
/// </summary>
public sealed class SectionContentExpansionAgent
{
    private const string Instructions = """
        You are a subject-matter professor writing real, substantial
        explanatory content for ONE topic/section of a course, given only
        its outline label (e.g. "Week 1: Alphabet Review, Letter Names,
        Letter Sounds") — the source document itself only lists this as a
        bare topic heading, with no developed prose, so you must write the
        actual teaching content from your own subject knowledge.

        Write 2-5 paragraphs of real, accurate, age/level-appropriate
        explanatory content that a student could actually learn from —
        concepts explained, at least one concrete example, never just a
        restatement of the topic label itself. Stay strictly within the
        scope of the given topic label and its chapter/course context —
        never invent unrelated content, never pad with generic filler
        ("this is an important topic..."), never mention that you are an
        AI or that the source lacked content.

        If WebGroundingFindings are provided, you may draw on them for
        current/verifiable facts, citing them naturally in prose (not as a
        separate reference list) — but you are not limited to them; use
        your own general subject knowledge for anything they don't cover.

        Write in the requested Language. Return ONLY the body content —
        no title, no headings repeating the topic label, no meta-commentary.
        """;

    private readonly AIAgent? _mainAgent;
    private readonly AIAgent? _economyAgent;
    private readonly string _mainModelName;
    private readonly string _economyModelName;

    public SectionContentExpansionAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var mainDeploymentName = configuration["AzureOpenAIDeploymentName"];
        var economyDeploymentName = configuration["AzureOpenAIEconomyDeploymentName"] ?? mainDeploymentName;
        var apiKey = configuration["AzureOpenAIApiKey"];

        _mainModelName = mainDeploymentName ?? string.Empty;
        _economyModelName = economyDeploymentName ?? string.Empty;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(mainDeploymentName))
        {
            _mainAgent = null;
            _economyAgent = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _mainAgent = client.GetChatClient(mainDeploymentName).AsAIAgent(instructions: Instructions, name: "SectionContentExpansionAgent");
        _economyAgent = string.IsNullOrWhiteSpace(economyDeploymentName)
            ? _mainAgent
            : client.GetChatClient(economyDeploymentName).AsAIAgent(instructions: Instructions, name: "SectionContentExpansionAgent-Economy");
    }

    public bool IsConfigured => _mainAgent is not null;

    public async Task<SectionContentExpansionResult> ExpandAsync(
        string topicLabel,
        string chapterTitle,
        string documentContext,
        string languageCode,
        ContentExpansionModelChoice modelChoice,
        string? webGroundingFindings,
        CancellationToken cancellationToken = default)
    {
        var agent = modelChoice == ContentExpansionModelChoice.Economy ? _economyAgent : _mainAgent;
        var modelName = modelChoice == ContentExpansionModelChoice.Economy ? _economyModelName : _mainModelName;

        if (agent is null)
        {
            throw new InvalidOperationException(
                "SectionContentExpansionAgent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var targetLanguageName = languageCode.Trim().ToLowerInvariant() == "en" ? "English" : "Spanish";
        var prompt =
            $"Course/document context: {documentContext}\n" +
            $"Chapter: {chapterTitle}\n" +
            $"Topic label (outline entry with no developed content): {topicLabel}\n" +
            $"Language: {targetLanguageName}\n" +
            (string.IsNullOrWhiteSpace(webGroundingFindings) ? string.Empty : $"\nWebGroundingFindings:\n{webGroundingFindings}\n");

        var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken);

        var usage = response.Usage;
        return new SectionContentExpansionResult
        {
            ExpandedText = response.Text,
            TokenUsage = new AgentTokenUsage
            {
                AgentName = "SectionContentExpansion",
                ModuleId = topicLabel,
                ModelName = modelName,
                InputTokens = (int)(usage?.InputTokenCount ?? 0),
                OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
                CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
            }
        };
    }
}
