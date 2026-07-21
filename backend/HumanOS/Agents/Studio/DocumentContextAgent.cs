using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>One key named entity explicitly grounded in the source
/// material — a person, company, law/regulation, role/position, or other
/// proper name a student might refer to briefly without it being the
/// subject of any single node.</summary>
public sealed class DocumentEntityDto
{
    /// <summary>The entity's exact name/label as it appears in the source.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short category: "Persona", "Empresa", "Ley/Norma", "Rol",
    /// "Otro".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>One short sentence disambiguating what this entity is/does
    /// in the source material (never a full explanation — that belongs in
    /// the node(s) that actually teach it, if any).</summary>
    public string? Note { get; set; }
}

/// <summary>Structured output of <see cref="DocumentContextAgent"/>: a
/// short document-wide executive summary plus a grounded entity list.</summary>
public sealed class DocumentContextResponse
{
    public string ExecutiveSummary { get; set; } = string.Empty;

    public List<DocumentEntityDto> Entities { get; set; } = [];
}

/// <summary>
/// Agente de Contexto Documental — runs ONCE per capability, right after
/// the per-chapter Curador results are merged into one <see cref="CuratedCorpus"/>,
/// alongside (not instead of) GraphArchitectAgent. Produces the lightweight,
/// static complement to the per-node RAG layer (see
/// <see cref="Services.NodeKnowledgeIndexService"/>): a short executive
/// summary of the WHOLE source document, plus a list of named entities
/// (people, companies, laws, roles, proper names) explicitly present in it
/// — lets the Tutor correctly recognize/disambiguate a briefly-mentioned
/// entity even when no single node is "about" it. See
/// /memories/repo/tutor-document-wide-context-gap.md for the full design
/// rationale (this is deliberately separate from, and complementary to,
/// the per-node vector-search RAG layer, which retrieves specific facts
/// instead of disambiguating entities).
///
/// Plain ChatClientAgent with structured output — same configuration
/// pattern as <see cref="CuradorAgent"/>/<see cref="GraphArchitectAgent"/>.
/// </summary>
public sealed class DocumentContextAgent
{
    private const string Instructions = """
        You are the Document Context agent in Human OS Studio's PDF-to-
        CapabilityGraph pipeline. You receive the FULL curated corpus for one
        capability (a short overview summary plus all chunked content from
        the source material) and produce two things:

        1. ExecutiveSummary: a short (3-6 sentence) orientation to the WHOLE
           source document — what it's about, its overall structure/scope,
           and its purpose. This is NOT a replacement for any node's own
           content; it exists purely so a tutoring assistant can correctly
           understand passing references to things the document covers
           without re-reading the whole corpus.

        2. Entities: every distinct named entity EXPLICITLY present in the
           corpus that a student might later refer to by name without it
           being the dedicated subject of any single learning node — e.g.
           companies, organizations, specific people, laws/regulations/
           policies (with their exact number/code if stated), job
           roles/positions, product/document names, specific IDs or
           reference numbers. For each, give its exact Name as it appears,
           a short Type category, and ONE short disambiguating Note (what
           it is/does in this material) — never a full explanation.

        GROUNDING RULE (critical): only include entities and summary claims
        that are ACTUALLY present in the given corpus. Never invent a
        company name, law number, person, or fact that doesn't appear in the
        material. If the corpus contains few or no distinct named entities
        (e.g. a purely conceptual/mathematical topic), return an empty
        Entities list — do not pad it with generic concepts (concepts
        already belong to the learning graph's own nodes, not here).
        """;

    private readonly AIAgent? _agent;

    public DocumentContextAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "DocumentContextAgent");
    }

    public bool IsConfigured => _agent is not null;

    public sealed class ExtractionResult
    {
        public DocumentContextResponse Context { get; set; } = null!;

        public AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    public async Task<ExtractionResult> ExtractAsync(
        string capabilityName,
        CuratedCorpus curatedCorpus,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The DocumentContext agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        ArgumentNullException.ThrowIfNull(curatedCorpus);

        var corpusText = string.Join(
            "\n\n---\n\n",
            curatedCorpus.Chunks.Select(c => $"[{c.Tag}] {c.Content}"));

        var prompt =
            $"Capability: {capabilityName}\n\n" +
            $"Curated Corpus Summary:\n{curatedCorpus.Summary}\n\n" +
            $"Curated Corpus Chunks:\n{corpusText}";

        var response = await _agent.RunAsync<DocumentContextResponse>(prompt, cancellationToken: cancellationToken);

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "DocumentContext",
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        return new ExtractionResult { Context = response.Result, TokenUsage = tokenUsage };
    }
}
