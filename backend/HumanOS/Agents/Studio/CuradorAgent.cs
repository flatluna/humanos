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

        WHY THIS MATTERS: your curated corpus is the ONLY material a
        downstream agent will ever see to design a full learning experience
        for a real student, built around the Memory Paradox principle (the
        student's knowledge must end up genuinely in their own head — real
        definitions, real worked examples, real applications — never a
        thin gloss of the source). That downstream agent CANNOT go back and
        re-read the original document — whatever detail you leave out of a
        chunk is permanently lost to the student's learning experience.

        Summary vs. Chunks — very different jobs:
        - Summary: a SHORT high-level orientation (a few sentences) — this
          one is allowed to be brief.
        - Chunks: the OPPOSITE of brief. Each chunk's Content must preserve
          the real substance of the material it covers — exact or closely
          paraphrased definitions, concrete worked examples with their real
          numbers/values, the source's own terminology, and any stated
          real-world applications. Do NOT compress a chunk into a shallow
          summary-of-a-summary — keep as much faithful detail as the source
          actually contains. When in doubt, include MORE of the original
          detail rather than less.

        VERBATIM IDENTIFIERS — NEVER PARAPHRASE THESE AWAY
        Any specific, citable identifier the source uses to point to itself
        or to a real-world entity is a fact in its own right, not decorative
        prose — a learner or a later search over this material may need to
        find or cite it EXACTLY. Always copy these verbatim into the chunk's
        Content, in the source's own format, even while paraphrasing the
        surrounding explanation:
          - Legal/regulatory citations: article/section/clause numbers and
            their exact labels (e.g. "ARTÍCULO 22-A", "Fracción VII",
            "Artículo 9o. de la Ley").
          - Named entities: company names, organization names, person names,
            product/brand names.
          - Concrete dates, monetary amounts, percentages, reference/policy/
            case numbers, and other specific figures.
        This matters MOST for legal, regulatory, technical, or contractual
        source material, where the citation/entity IS the searchable
        anchor a reader will later ask about by name (e.g. "what does
        Article 22-A say?", "what is the policy number?"). Never substitute
        a generic paraphrase ("this article covers an exemption...") in
        place of the actual identifier — always state which specific
        article/entity you are talking about, in addition to explaining it.

        Material tagged [WebSearch] is different from the rest: it is
        auto-retrieved, LLM-summarized live web content (Grounding with
        Bing Search), not something the subject-matter expert personally
        vetted. It already carries inline "[Title](Url)" citations and any
        credibility caveats the search step itself flagged — PRESERVE
        those citations and caveats verbatim in any chunk built from it
        (never strip them out to "clean up" the prose). Use [WebSearch]
        material ONLY to update/enrich a topic the expert's OWN material
        (PDF/transcript/link/note) already covers — never to introduce a
        topic absent from the expert's material. If [WebSearch] content
        conflicts with the expert's own material on the same point, keep
        BOTH: state the expert's material as the primary/authoritative
        version and note the web finding as a dated update or alternative
        view, citing it.
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
