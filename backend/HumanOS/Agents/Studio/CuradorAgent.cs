using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

public sealed class CuratedChunk
{
    /// <summary>Short topic label useful for later level/module design.</summary>
    public string Tag { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

public sealed class CuratedCorpus
{
    public string Summary { get; set; } = string.Empty;

    public List<CuratedChunk> Chunks { get; set; } = [];
}

/// <summary>
/// Agente Curador — first step of the Human OS Studio capability-creation
/// pipeline (see /memories/repo/humanstudio-multiagent-vision.md).
/// Organizes the raw material the user feeds in (PDFs, video transcripts,
/// web links, notes) into a curated, chunked, tagged corpus that the
/// Agente Arquitecto designs the capability's levels from.
///
/// Plain ChatClientAgent with structured output — no Harness/Skills.
/// Harness+Skills is reserved for the runtime Agente-Tutor only.
/// Follows the same configuration pattern as
/// <see cref="JobDescriptionExtractionAgent"/>.
/// </summary>
public sealed class CuradorAgent
{
    private const string Instructions = """
        You are the Curator agent in Human OS Studio's capability-creation
        pipeline. You receive raw material provided by a subject-matter
        expert (PDFs, video transcripts, web links, personal notes) and
        organize it into a curated corpus: a short overall summary, plus a
        set of tagged chunks grouping related content together. Only use
        information present in the provided material — never invent facts.
        Tag each chunk with a short topic label useful for later designing
        a capability's learning levels.
        """;

    private readonly AIAgent? _agent;

    public CuradorAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

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
            .AsAIAgent(instructions: Instructions, name: "CuradorAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>Result of a Curador call: the curated corpus plus the
    /// token usage of the call that produced it (observability only).</summary>
    public sealed class CurationResult
    {
        public CuratedCorpus Corpus { get; set; } = null!;

        public AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    public async Task<CurationResult> CurateAsync(
        IReadOnlyList<RawMaterialItem> rawMaterials,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Curador agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var prompt = "Organize the following raw material into a curated corpus:\n\n" +
            string.Join(
                "\n\n---\n\n",
                rawMaterials.Select(m => $"[{m.Type}] {m.Label}\n{m.Content}"));

        var response = await _agent.RunAsync<CuratedCorpus>(prompt);

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "Curador",
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        return new CurationResult { Corpus = response.Result, TokenUsage = tokenUsage };
    }
}
