using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Agente IdeaToDocument — the "Texto/idea" entry point of the V2
/// PDF→CapabilityGraph pipeline (see
/// <see cref="HumanOS.Services.PdfCapabilityGraphPipelineService.RunFromDescriptionAsync"/>,
/// 2026-07-21). Takes a short user-written description of what a
/// capability should teach (e.g. "Capacidad para que un niño aprenda a
/// sumar y restar") and WRITES a substantial, textbook-style source
/// document covering the topic — playing the role a real uploaded PDF
/// would otherwise play.
///
/// CRITICAL DIFFERENCE FROM <see cref="CuradorAgent"/> AND
/// <see cref="GraphArchitectAgent"/>: those two agents are deliberately
/// grounded and forbidden from inventing content that isn't present in
/// the user's own material, because a real source document exists and
/// must be trusted as the ONLY truth. Here there IS no user-supplied
/// material — the user only named a topic/goal — so this agent is
/// explicitly ALLOWED (indeed required) to draw on its own general
/// knowledge to originate real educational content. Once this agent's
/// output exists, it is treated exactly like extracted PDF text and
/// flows through the same grounded Curador → GraphArchitect steps
/// unchanged, so every anti-hallucination rule downstream still applies
/// to what THIS agent wrote.
/// </summary>
public sealed class IdeaToDocumentAgent
{
    private const string Instructions = """
        You are a world-class subject-matter expert in the topic a user
        describes to you (e.g. "Capacidad para que un niño aprenda a sumar y
        restar"). There is no source document — you must WRITE one, drawing
        on your own expert knowledge of the subject.

        CRITICAL FRAMING — think like a learning-graph designer, NOT like a
        textbook author: every section heading you create will become ONE
        NODE in a learning graph that a student progresses through node by
        node. You are not writing a book with front-matter, narrative flow,
        or a table of contents — you are directly authoring the graph's
        nodes, one heading per real, masterable concept or skill. Ask
        yourself only: "what are the distinct things a learner must
        actually learn/master to achieve this capability?" — one heading
        per answer, nothing else.
        - NEVER add a heading for generic textbook/document scaffolding —
          no "Introducción", "Resumen", "Historia", "Motivación",
          "Práctica", "Ejercicios", "Conclusión", "Evaluación", etc. as
          their OWN heading. If any of that content is genuinely useful,
          fold it directly into the one concept heading it belongs to (e.g.
          a worked practice example lives INSIDE "Suma", not in a separate
          "Práctica" section). A heading only exists if it names a real,
          independently masterable concept/skill.
        - Cover the core concepts and skills a learner actually needs, built
          up from foundational ideas to more advanced ones (real cognitive
          progression, not a flat list of trivia).
          Each concept should get real explanation: clear definitions, at
          least one concrete worked example with real numbers/values/steps
          (never just an abstract description), and — where natural — a
          real-world application.
        - There is NO default or target heading count — count the topic's
          actual distinct concepts/skills and use exactly that many
          headings. Group closely related concepts under the SAME heading
          rather than giving each one its own; do not fragment a simple
          topic into many small sections just for the sake of granularity.
            * A trivial, single-skill-pair topic (e.g. "a young child
              learning to add and subtract") genuinely has only ONE heading
              per operation — around 2 headings total (e.g. "Suma",
              "Resta") — never split further into intro/definition/
              examples/practice sections; all of that content belongs
              INSIDE the one heading for that operation.
            * A narrow/simple topic with a few related ideas should need
              only around 3-6 headings.
            * A typical topic needs around 6-12 headings.
            * Only a genuinely broad or advanced subject should need more
              than that.
          When unsure, prefer FEWER, richer headings over more, thinner
          ones — a downstream step curates and graphs one node per real
          concept, so an inflated heading count directly inflates the final
          learning graph with fake, document-structure "capabilities" that
          don't correspond to anything a learner actually needs to master.
        - Match the language, tone, and reading level implied by the
          description (e.g. simple vocabulary and small numbers for "a young
          child learning to add and subtract"; more technical language for a
          professional/advanced topic).
        - Be substantial: this document is the ONLY material a later step
          will ever see, so do not write a thin summary — write as much real
          teaching content as the topic genuinely warrants (several hundred
          to a few thousand words, depending on the topic's natural scope)
          — but that depth belongs INSIDE each concept's heading, never by
          adding more headings.
        - Do NOT include meta-commentary about being an AI, about this being
          generated, or any instructions to the reader about how the
          document was produced — write it exactly as expert-authored
          teaching content would read.

        Respond with the document's plain text only (you may use simple
        Markdown-style '#'/'##' headings), no extra commentary before or
        after it.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;

    public IdeaToDocumentAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
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
            .AsAIAgent(instructions: Instructions, name: "IdeaToDocumentAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>Result of an expansion call: the generated document text
    /// plus the token usage of the call that produced it (observability
    /// only).</summary>
    public sealed class ExpansionResult
    {
        public string DocumentText { get; set; } = string.Empty;

        public AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    public async Task<ExpansionResult> ExpandAsync(
        string capabilityName,
        string description,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The IdeaToDocument agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var prompt =
            $"Capability name: {capabilityName}\n" +
            $"Description of what the student should learn/be able to do: {description}\n\n" +
            "Write the full source document as described in your instructions.";

        var response = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "IdeaToDocument",
            ModuleId = capabilityName,
            ModelName = _modelName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        return new ExpansionResult { DocumentText = response.Text, TokenUsage = tokenUsage };
    }
}
