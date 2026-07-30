using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Agent for Capability Studio's "professor" tool — Studio → CapabilityGraph
/// preview → "Agregar nodo": designs ONE new learning node (concept or skill)
/// to be inserted into an ALREADY-PUBLISHED CapabilityGraph, from a
/// professor's free-text prompt plus the graph's existing structure.
///
/// Deliberately separate from <see cref="GraphArchitectAgent"/> (which
/// designs a WHOLE graph from a curated corpus): this agent designs exactly
/// one node, is grounded by the professor's own instruction (not a corpus),
/// and must respect the surrounding graph it is being inserted into (avoid
/// duplicate names, stay coherent with the chosen prerequisite/dependent
/// nodes). Reuses <see cref="GraphNodeDto"/> so downstream enrichment
/// (AcademicDefinition/Interpretation/Examples/Applications/
/// IllustrationPrompts) and persistence stay identical to the main pipeline.
///
/// Plain ChatClientAgent with structured output — same construction pattern
/// as every other Studio agent (see CuradorAgent/GraphArchitectAgent).
/// </summary>
public sealed class GraphNodeAdditionAgent
{
    private const string Instructions = """
        You are the Graph Node Addition agent in Human OS Studio. A professor
        is authoring/extending a Capability Graph that is ALREADY PUBLISHED and
        may already have real students progressing through it. Your job is to
        design EXACTLY ONE new learning node (a concept or skill) from the
        professor's free-text instruction, so it can be inserted into that
        existing graph.

        THE #1 RULE: A NODE IS A CAPABILITY, NOT A DOCUMENT SECTION
        The node must represent something a HUMAN can come to understand or
        do — a concept they can grasp, or a skill they can perform. Never a
        section, chapter, lesson, or document artifact.

        CONTEXT YOU RECEIVE
        - The capability's name/description (what the whole graph is about).
        - The names of every node ALREADY in the graph — your new node's Name
          MUST be different from every one of them (case-insensitive), and its
          underlying idea should not simply duplicate an existing node either.
        - The specific nodes the professor chose as this new node's
          PREREQUISITES ("requires") — nodes the student must already know
          before this new node, if any.
        - The specific nodes the professor chose as this new node's
          DEPENDENTS ("required by") — existing nodes that will now depend on
          this new node once it exists, if any.
        - The professor's own free-text instruction describing what the node
          should be about.

        Use the prerequisite/dependent context to make sure the new node's
        content is coherent with its place in the graph (e.g. if it requires
        "Fracciones" and is required by "Suma de Fracciones", the new node
        should sit conceptually between those two).

        NAMING RULES
        - 2 to 5 words, human/knowledge-centered (never "Introducción a...",
          "Sección sobre...", "Lección de...").
        - MUST be distinct from every existing node name given to you.

        A NODE HOLDS ITS OWN COMPLETE KNOWLEDGE
        Never split the node's definition/interpretation/examples/applications
        into separate nodes — this ONE node must be fully self-contained:
        - AcademicDefinition: a precise, formal definition.
        - Interpretation: the same idea in plain, intuitive language.
        - Examples: concrete worked examples that make it tangible.
        - Applications: real-world situations where it gets used.
        - IllustrationPrompts: when an image genuinely helps, write EXACTLY
          TWO prompts (never one, never more than two) — one Purpose=Hypothesis
          (depicts ONLY the "before"/given state, no answer/result of any kind
          revealed) and one Purpose=Teaching (a full worked example with the
          real, resolved answer, using the SAME underlying scenario/values as
          the Hypothesis prompt). Leave the list EMPTY if no image genuinely
          helps this node. Never invent unstated numeric data in a prompt that
          depicts charts/scores/percentages — spell out exact, internally
          consistent numbers directly in the prompt text.
        - References: leave empty (this node isn't grounded in a curated
          corpus chunk — it comes from the professor's own instruction).

        CLASSIFICATION
        - NodeType=Concept: theoretical knowledge the learner comes to
          understand.
        - NodeType=Skill: a practical ability the learner can perform.

        NeedsCurrentInfo
        Set true ONLY if the correct answer for this node's content could
        plausibly be DIFFERENT if asked again in a year, because the
        underlying facts genuinely evolve (current tools/frameworks, industry
        trends, pricing, evolving regulations). Default to false for timeless
        concepts/skills (arithmetic, core definitions, stable laws/theorems,
        grammar rules, etc.).

        OUTPUT FORMAT
        Return a single GraphNodeDto JSON object (NOT a list, NOT a full
        graph) with: Name, Description, NodeType, AcademicDefinition,
        Interpretation, Examples, Applications, IllustrationPrompts (each with
        Purpose + Prompt), References (empty list), NeedsCurrentInfo. Do not
        set SortOrder — the system assigns it automatically.

        MEMORY PARADOX: this node must represent real, masterable human
        knowledge/ability — never a proxy for how the professor happened to
        phrase the request.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;

    /// <summary>Same persona block as <see cref="ExperienceDesignerAgent.YoungLearnerPersonaBlock"/>
    /// (kept as a separate literal since the two agents/prompts are otherwise
    /// independent) — applied ONLY when the owning Capability's
    /// PedagogicalAudience is "YoungLearner". Affects only the VOICE of
    /// Interpretation/Examples/Applications, never the node structure/rules
    /// above.</summary>
    private const string YoungLearnerPersonaBlock = """
        AUDIENCE PERSONA — YOUNG LEARNER MODE (this capability is for young
        children, roughly ages 5-8, e.g. Kindergarten/Grade 1). Write
        Interpretation/Examples/Applications in warm, simple words a young
        child already knows, as if a friendly teacher is speaking directly
        to the child — never formal/academic register. This changes only
        the VOICE, never the required fields/structure above.
        """;

    public GraphNodeAdditionAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "GraphNodeAdditionAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>Result of a node-addition call: the designed node plus the
    /// token usage of the call that produced it (observability only).</summary>
    public sealed class NodeAdditionResult
    {
        public GraphNodeDto Node { get; set; } = null!;

        public AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    /// <summary>
    /// Designs one new node to be inserted into an existing capability graph.
    /// </summary>
    public async Task<NodeAdditionResult> GenerateNodeAsync(
        string capabilityName,
        string? capabilityDescription,
        IReadOnlyList<string> existingNodeNames,
        IReadOnlyList<(string Name, string? Description)> requiresNodes,
        IReadOnlyList<(string Name, string? Description)> requiredByNodes,
        string professorPrompt,
        string languageCode = "es",
        string? pedagogicalAudience = null,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The GraphNodeAddition agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        if (string.IsNullOrWhiteSpace(professorPrompt))
            throw new ArgumentException("professorPrompt is required", nameof(professorPrompt));

        static string DescribeNodeList(IReadOnlyList<(string Name, string? Description)> nodes) =>
            nodes.Count == 0
                ? "(none)"
                : string.Join("\n", nodes.Select(n => $"- {n.Name}" + (string.IsNullOrWhiteSpace(n.Description) ? "" : $": {n.Description}")));

        var personaSection = string.Equals(pedagogicalAudience, "YoungLearner", StringComparison.OrdinalIgnoreCase)
            ? $"\n\n{YoungLearnerPersonaBlock}"
            : string.Empty;

        var prompt =
            $"Capability: {capabilityName}\n" +
            (string.IsNullOrWhiteSpace(capabilityDescription) ? "" : $"Capability description: {capabilityDescription}\n") +
            $"\nExisting node names in this graph (the new node's Name must differ from ALL of these):\n" +
            (existingNodeNames.Count == 0 ? "(none)" : string.Join(", ", existingNodeNames)) +
            $"\n\nNodes the new node must REQUIRE (its prerequisites):\n{DescribeNodeList(requiresNodes)}" +
            $"\n\nExisting nodes that will now REQUIRE the new node (its dependents):\n{DescribeNodeList(requiredByNodes)}" +
            $"\n\nProfessor's instruction for the new node:\n{professorPrompt}" +
            personaSection +
            $"\n\nTarget output language: {(languageCode.Trim().ToLowerInvariant() == "en" ? "English" : "Spanish")}. " +
            "Write Name/Description/AcademicDefinition/Interpretation/Examples/Applications in this language.";

        var response = await _agent.RunAsync<GraphNodeDto>(prompt, cancellationToken: cancellationToken);
        var node = response.Result;

        if (node.NodeId == Guid.Empty)
            node.NodeId = Guid.NewGuid();

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "GraphNodeAddition",
            ModelName = _modelName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        return new NodeAdditionResult { Node = node, TokenUsage = tokenUsage };
    }
}
