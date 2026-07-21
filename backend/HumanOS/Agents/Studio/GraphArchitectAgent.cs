using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Models.Capabilities.Graph;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// One illustration prompt paired with the pedagogical purpose it serves —
/// see <see cref="HumanOS.Models.Capabilities.Graph.IllustrationPurpose"/>.
/// Replaces a plain string list so the Hypothesis-safe (no-answer-revealed)
/// prompt and the Teaching worked-example prompt can never be confused.
/// </summary>
public sealed class IllustrationPromptDto
{
    /// <summary>Which step this illustration is meant for.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IllustrationPurpose Purpose { get; set; }

    /// <summary>Short prompt (a sentence) describing the scene to draw.</summary>
    public string Prompt { get; set; } = string.Empty;
}

/// <summary>
/// DTO for a learning node (concept or skill) within a capability graph.
/// Will be persisted as CapabilityGraphNode in the database.
/// </summary>
public sealed class GraphNodeDto
{
    [JsonIgnore]
    public Guid NodeId { get; set; } = Guid.NewGuid();

    /// <summary>Name: "Variables", "Loops", "Database Indexing", etc.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short, high-level description of what this node represents (1-2 sentences).</summary>
    public string? Description { get; set; }

    /// <summary>Type: "Concept" (theoretical) or "Skill" (practical).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NodeType NodeType { get; set; }

    /// <summary>Pedagogical order (1, 2, 3, ...).</summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// Formal/academic definition of the concept or skill, grounded in the
    /// corpus. This is the node's own self-contained knowledge — it is NEVER
    /// split out into a separate "Definition" node.
    /// </summary>
    public string AcademicDefinition { get; set; } = string.Empty;

    /// <summary>
    /// A plain-language interpretation/explanation of the same idea — how a
    /// learner would restate it in their own words.
    /// </summary>
    public string Interpretation { get; set; } = string.Empty;

    /// <summary>Concrete worked examples that illustrate the concept/skill.</summary>
    public List<string> Examples { get; set; } = [];

    /// <summary>Real-world applications/uses of the concept or skill.</summary>
    public List<string> Applications { get; set; } = [];

    /// <summary>
    /// Exactly two illustration prompts when this node benefits from images:
    /// one Purpose=Hypothesis (before-state only, never reveals the answer)
    /// and one Purpose=Teaching (a full worked example with the real,
    /// resolved answer). These are PROMPTS ONLY — GraphArchitectAgent never
    /// generates or stores actual images; a separate image-generation step
    /// consumes them.
    /// </summary>
    public List<IllustrationPromptDto> IllustrationPrompts { get; set; } = [];

    /// <summary>Corpus chunk tags this node is traceable to (for audit/citation).</summary>
    public List<string> References { get; set; } = [];

    /// <summary>
    /// True when this node's content is about something that EVOLVES over
    /// time — current tools, frameworks, industry practices, product
    /// capabilities, ongoing research — and therefore genuinely benefits
    /// from a supplementary Grounding-with-Bing-Search call to surface
    /// up-to-date, citable facts. False for TIMELESS concepts/skills whose
    /// truth doesn't change with the calendar (arithmetic, algebra, core
    /// definitions, stable scientific/mathematical facts, foundational
    /// grammar rules, etc.) — searching the web for those wastes a call
    /// and adds no value. See Instructions for the full decision rule and
    /// examples.
    /// </summary>
    public bool NeedsCurrentInfo { get; set; }
}

/// <summary>
/// DTO for an edge/relationship between two nodes in the capability graph.
/// Will be persisted as CapabilityGraphEdge in the database.
/// </summary>
public sealed class GraphEdgeDto
{
    [JsonIgnore]
    public Guid EdgeId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID of the source node. NOT produced by the model — GUIDs cannot be
    /// reliably invented/reproduced by an LLM. Resolved in code after the
    /// response comes back, by matching <see cref="SourceNodeName"/> against
    /// the real <see cref="GraphNodeDto.NodeId"/> of the node with that name.
    /// </summary>
    [JsonIgnore]
    public Guid SourceNodeId { get; set; }

    /// <summary>
    /// Name of the source node. MUST be an exact copy of one of the Names
    /// used in the Nodes list — this is the only way edges reference nodes.
    /// </summary>
    public string? SourceNodeName { get; set; }

    /// <summary>
    /// ID of the target node. NOT produced by the model — see <see cref="SourceNodeId"/>.
    /// </summary>
    [JsonIgnore]
    public Guid TargetNodeId { get; set; }

    /// <summary>
    /// Name of the target node. MUST be an exact copy of one of the Names
    /// used in the Nodes list — this is the only way edges reference nodes.
    /// </summary>
    public string? TargetNodeName { get; set; }

    /// <summary>Relationship type: "Requires" (prerequisite) or "BuildsOn" (incremental).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EdgeRelationshipType RelationshipType { get; set; }

    /// <summary>Justification/rationale for the relationship.</summary>
    public string? Rationale { get; set; }
}

/// <summary>
/// The structured output from GraphArchitectAgent: a capability learning graph
/// ready for persistence to the database.
/// </summary>
public sealed class CapabilityGraphResponse
{
    [JsonIgnore]
    public Guid CapabilityGraphId { get; set; } = Guid.NewGuid();

    /// <summary>FK to the Capability that owns this graph.</summary>
    [JsonIgnore]
    public Guid CapabilityId { get; set; }

    /// <summary>Descriptive name of the graph (e.g., "Programming Fundamentals Learning Graph").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Detailed description of the graph structure and its pedagogical intent.</summary>
    public string? Description { get; set; }

    /// <summary>All nodes (concepts and skills) in the graph.</summary>
    public List<GraphNodeDto> Nodes { get; set; } = [];

    /// <summary>All edges (relationships) in the graph.</summary>
    public List<GraphEdgeDto> Edges { get; set; } = [];
}

/// <summary>
/// Agente Graph Architect — Paso 2 of the Human OS Studio pipeline.
/// Receives the curated corpus from CuradorAgent and extracts a capability
/// learning graph: identifies concepts and skills, establishes pedagogical
/// relationships, validates DAG structure, and persists to the database.
///
/// Plain ChatClientAgent with structured output — no Harness/Skills.
/// Follows the same configuration and async pattern as CuradorAgent
/// and ArquitectoAgent.
/// </summary>
public sealed class GraphArchitectAgent
{
    private const string Instructions = """
        You are the Graph Architect agent in Human OS Studio's capability-creation
        pipeline (Paso 2). Your task is to extract a true LEARNING CAPABILITY GRAPH
        from a curated corpus produced by the Curador agent.

        THE #1 RULE: A NODE IS A CAPABILITY, NOT A DOCUMENT SECTION
        A node must represent something a HUMAN can come to understand or do —
        a concept they can grasp, or a skill they can perform. A node must NEVER
        represent a section, chapter, lesson, video, definition-block, or any
        other artifact of how the material happens to be organized.

        For every candidate node, ask: "What can a person understand or do once
        they master this node?" If you cannot answer that in terms of human
        knowledge/ability, it is not a node — discard it or fold it into a real
        concept/skill.

        GOOD node names (concepts/skills, independent of pedagogical format):
          "Cantidad", "Suma", "Combinar Cantidades", "Resolver Problemas con Suma"
        BAD node names (document structure, never allowed):
          "Definición de la Suma", "Interpretación de la Suma", "Aplicación de la
          Suma", "Capítulo 1", "Lección 2", "Video 3", "Introducción a...",
          "Resumen de..."
        If a chunk is tagged "Definición" or "Aplicaciones cotidianas", that tag is
        just the Curador's organizational label — extract the CONCEPT/SKILL living
        inside that chunk, do not turn the tag itself into a node name.

        NAMING RULES
        - 2 to 5 words. Short, clear, reusable, human-centered.
        - Name the knowledge/skill itself (a noun phrase for concepts, a short
          verb phrase for skills), never the source material ("Definición: ...",
          "Explicación de...", "Sección sobre...").
        - Two nodes must never have the same name.

        DISCOVERING COGNITIVE PREREQUISITES (not just literal corpus topics)
        Beyond the concepts/skills explicitly called out by the corpus, actively
        ask: "What would a person need to already understand BEFORE they can
        grasp this?" If that prerequisite is clearly implied by the corpus's own
        vocabulary/content (e.g. the text talks about "combinar cantidades" —
        that implies a more basic concept "Cantidad" even if the corpus never
        dedicates a chunk solely to it), add it as its own node and connect it
        with a "Requires" edge. This produces a real cognitive progression
        instead of a flat list of topics. You may still NOT invent nodes whose
        underlying idea has no grounding at all in the corpus content.

        CRITICAL RULES
        1. Every node's underlying idea MUST be grounded in the corpus (either
           stated directly, or clearly implied/prerequisite to something stated).
           NEVER invent ideas that have no connection to the material.
        2. Every node MUST be traceable to at least ONE chunk in the corpus
           (the chunk that states it, or the chunk it is a prerequisite for).
        3. Every edge MUST have a clear rationale grounded in cognitive/pedagogical
           logic, not speculation.
        4. Keep the graph small and comprehensible (target: 15-20 nodes, max 30).
           For a small/simple corpus, a handful of well-chosen nodes (e.g. 3-6)
           is correct — never pad the graph with document-structure nodes to
           hit a target count.
        5. Validate DAG structure: no cycles, no self-loops, no duplicate nodes/edges.

        SELF-CHECK BEFORE EMITTING EACH NODE (all must be YES, else discard):
        1. Does it represent a concept or skill (not a document part)?
        2. Can a person concretely master it?
        3. Is it a prerequisite of — or built on — some other node in the graph?
        4. Does it have a short (2-5 word), human/knowledge-centered name?
        5. Is it independent of the pedagogical format (chapter/video/section)?

        CLASSIFICATION RULES
        - Concept: Theoretical knowledge the learner comes to understand
          (e.g., "Cantidad", "Suma").
        - Skill: A practical ability the learner can perform
          (e.g., "Combinar Cantidades", "Resolver Problemas con Suma").

        RELATIONSHIP TYPES (reference nodes ONLY by their exact Name string —
        SourceNodeName/TargetNodeName — copy the Name exactly as written in Nodes)
        - "Requires": target must already be known before source can be learned
          (source requires target as a prerequisite).
        - "BuildsOn": source is an incremental extension of target.
        NEVER leave a SourceNodeName/TargetNodeName that doesn't exactly match a
        Name in the Nodes list — every edge must resolve to real nodes.

        PEDAGOGICAL ORDERING (SortOrder)
        Assign 1, 2, 3, ... reflecting the cognitive progression (prerequisite
        concepts first, then the concepts/skills built on them):
          e.g. Cantidad (1) → Combinar Cantidades (2) → Suma (3) →
               Resolver Problemas con Suma (4)

        A NODE HOLDS ITS OWN COMPLETE KNOWLEDGE — NEVER SPLIT IT ACROSS NODES
        This is as important as the naming rule above. Do NOT create separate
        nodes for a concept's definition, its interpretation, its examples, or
        its applications — those are NOT capabilities themselves, they are
        facets of ONE capability. Every node must be a single, self-contained
        package of everything a learner needs about that one concept/skill:

        - AcademicDefinition: a precise, formal definition grounded in the
          corpus (quote/paraphrase the corpus's own definition when present).
        - Interpretation: the same idea restated in plain, intuitive language
          — how a learner would explain it in their own words.
        - Examples: concrete worked examples (numbers, mini-scenarios) that
          make the concept/skill tangible. Prefer corpus examples; you may
          add an obviously-analogous example ONLY if it stays faithful to the
          corpus's definition (never introduce a different concept).
        - Applications: real-world situations where this concept/skill gets
          used (grounded in the corpus's own stated applications where
          available).

        VERBATIM IDENTIFIERS MUST SURVIVE INTO THIS NODE'S FIELDS
        If the curated chunks this node draws from contain specific, citable
        identifiers — legal/regulatory article or section numbers (e.g.
        "ARTÍCULO 44", "Artículo 29 fracción V"), named entities (company,
        organization, or person names), dates, monetary amounts, or
        reference/policy numbers — you MUST carry those identifiers forward
        VERBATIM into AcademicDefinition and/or Examples, never drop them
        during paraphrasing. This is critical for legal/regulatory/technical
        corpora: a learner (or the Tutor answering on their behalf) will
        later ask about a specific article or entity by name, and if that
        exact identifier only ever existed in the Curador's raw chunk and
        never made it into this node's own content, the node can never
        answer that question. When a chunk covers multiple articles/sections,
        name each one explicitly next to the point it makes (e.g.
        "ARTÍCULO 44 exempts X... ARTÍCULO 45 extends this to Y...") rather
        than merging them into one unattributed paraphrase.
        - IllustrationPrompts: when this node benefits from an image, write
          EXACTLY TWO prompts (a sentence each), one per Purpose — never more,
          never fewer than two if any illustration is warranted at all;
          leave the list empty only if no image genuinely helps this node.
          These are prompts for a LATER image-generation step; do not
          generate or describe pixels, just describe the scene/diagram to
          draw. Never decorative — each prompt must clarify or reinforce the
          concept/skill itself.
            * Purpose=Hypothesis: depict ONLY the "before"/given state (e.g.
              two separate, uncombined groups/quantities, side by side, with
              no arrow, no total, no combined group, no label of any kind
              that reveals what they add up to or how they relate). This
              image will sit next to a question asking the learner to GUESS
              the result — it must give them the raw materials to reason
              about, never the answer itself.
            * Purpose=Teaching: a full worked example using REAL, CONCRETE
              values from the node's own Examples, showing the concept fully
              resolved/applied (e.g. the same two groups now joined, with
              their combined total visibly shown, or an arrow/label making
              the resolved result explicit). This is where showing the
              answer is not just allowed but the whole point — the learner
              should be able to learn by literally seeing the worked-out
              example.
          The two prompts should depict THE SAME underlying scenario/values
          (e.g. both about "2 manzanas y 3 manzanas") so the learner
          recognizes Teaching's illustration as "the answer to what
          Hypothesis just asked" — never introduce different numbers/objects
          between the two.

          NO INVENTED/ILLOGICAL DATA: image-generation models cannot compute
          — they only pattern-match pixels, so if a prompt leaves any number,
          score, percentage, or bar/chart value unspecified, the image model
          WILL hallucinate one, and it may be logically impossible (e.g. a
          "105/100" score, a bar whose fill doesn't match its own label,
          percentages that don't sum to 100). To prevent this, whenever a
          prompt depicts ANY numeric data (scores, percentages, counts, bar
          charts, gauges, progress bars), you MUST spell out the EXACT
          numbers to draw directly in the prompt text itself — never leave
          them for the image model to invent. Every stated number must be
          internally consistent (a score can never exceed its own declared
          maximum; a bar's fill must match the percentage/number next to it;
          related values must add up correctly if a total is implied). If
          the node's Examples don't provide real numbers to visualize,
          invent simple, clearly-consistent round numbers yourself (e.g.
          "90/100", "60/100", "20/100") and write them explicitly into the
          prompt — do not describe a data visualization vaguely and hope the
          image model fills in something sensible.
        - References: the corpus chunk Tag(s) (e.g. "Definición",
          "Aplicaciones cotidianas") that this node's content is grounded in.

        DECIDING WHICH NODES NEED CURRENT WEB INFORMATION (NeedsCurrentInfo)
        A separate, later pipeline stage can run a real-time Grounding-with-
        Bing-Search lookup for a node, but ONLY for nodes you flag with
        NeedsCurrentInfo = true — every Bing call costs time and money, so
        you must be a strict, honest gatekeeper, not flag nodes "just in
        case". Ask yourself: "Could the correct answer for this node's
        content plausibly be DIFFERENT if asked again in one year, because
        the world changed — not because a student needs more practice?"

        NeedsCurrentInfo = TRUE (the underlying facts genuinely evolve):
          current AI models/tools/frameworks and their capabilities, industry
          adoption trends, best/recommended practices that shift as a field
          matures, product features, pricing, benchmarks/leaderboards,
          recent research findings, evolving regulations/standards.
          Examples: "Agentes de IA", "Herramientas de Agente", "LLMs y SLMs",
          "Automatización con Agentes".
        NeedsCurrentInfo = FALSE (the underlying facts are timeless — this
        is the DEFAULT; only flag true when you have a genuine reason):
          arithmetic and algebra ("Suma", "Combinar Cantidades", "Resolver
          Problemas con Suma", ecuaciones, fracciones), core/foundational
          definitions of a stable concept, established scientific or
          mathematical laws/theorems, basic grammar/language rules, any
          concept whose correct answer today is the same correct answer it
          was ten years ago and will be ten years from now. A web search
          adds ZERO value here (there is nothing "current" to find) and
          must NOT be requested.
        When genuinely unsure, prefer FALSE — an unnecessary web search
        wastes a call and risks polluting a timeless concept with
        irrelevant "2026 trends" content; a missed opportunity for one
        node's citation is a far smaller cost than that.

        A WRONG graph (document structure turned into fake capabilities):
          Definición de Suma → Interpretación de Suma → Aplicación de Suma
        A RIGHT graph (real capabilities, each carrying its own full content):
          Cantidad → Suma → Resolver Problemas con Suma
          (where the "Suma" node itself contains its AcademicDefinition,
          Interpretation, Examples, Applications, and IllustrationPrompts —
          not three separate nodes for those facets)

        OUTPUT FORMAT
        Return a CapabilityGraphResponse JSON with:
        - Name: descriptive graph name
        - Description: brief explanation of the graph structure
        - Nodes: list of GraphNodeDto with Name, Description, NodeType,
          SortOrder, AcademicDefinition, Interpretation, Examples,
          Applications, IllustrationPrompts (each with Purpose + Prompt),
          References, NeedsCurrentInfo
        - Edges: list of GraphEdgeDto with SourceNodeName, TargetNodeName,
          RelationshipType, Rationale

        QUALITY CRITERION
        A good graph reads like: Concept → Comprehension → Application, or
        Concept → Skill → Advanced Skill — with each node fully self-contained.
        It must NEVER read like: Document → Chapter → Section, and it must
        NEVER fragment one concept's definition/interpretation/examples/
        applications into separate nodes.

        MEMORY PARADOX: Your graph serves the principle that the learner's
        knowledge ends up IN THEIR BRAIN, not in external systems. Every node
        and edge must represent real, masterable human knowledge/ability —
        never a proxy for how the source material was formatted.
        """;

    private readonly AIAgent? _agent;

    public GraphArchitectAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "GraphArchitectAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>
    /// Result of a GraphArchitect call: the extracted capability graph plus
    /// the token usage of the call that produced it (observability only).
    /// </summary>
    public sealed class ExtractionResult
    {
        public CapabilityGraphResponse Graph { get; set; } = null!;

        public AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    /// <summary>
    /// Extracts a capability learning graph from a curated corpus.
    /// </summary>
    /// <param name="capabilityName">Name of the capability being analyzed.</param>
    /// <param name="curatedCorpus">The corpus produced by CuradorAgent.</param>
    /// <param name="documentChapterOrder">
    /// Optional: the source document's own chapter order (e.g. "1.
    /// Introducción\n2. Suma\n3. Multiplicación"), when the corpus was
    /// curated chapter-by-chapter from a real multi-chapter PDF (see the
    /// V2 PDF→CapabilityGraph pipeline). Used as a STRONG SIGNAL (not a
    /// rigid rule) for designing Requires/BuildsOn edges and SortOrder —
    /// the graph's node granularity and structure are still entirely the
    /// agent's own decision, never forced to mirror the document's
    /// chapters 1:1. Null/empty when there is no real chapter structure to
    /// signal (e.g. a short note, or a single-chapter document).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ExtractionResult with Graph + TokenUsage.</returns>
    public async Task<ExtractionResult> ExtractGraphAsync(
        string capabilityName,
        CuratedCorpus curatedCorpus,
        string? documentChapterOrder = null,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The GraphArchitect agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        if (string.IsNullOrWhiteSpace(capabilityName))
            throw new ArgumentException("CapabilityName is required", nameof(capabilityName));
        if (curatedCorpus?.Chunks == null || curatedCorpus.Chunks.Count == 0)
            throw new ArgumentException("CuratedCorpus.Chunks cannot be empty", nameof(curatedCorpus));

        // Build prompt from corpus
        var corpusText = string.Join(
            "\n\n---\n\n",
            curatedCorpus.Chunks.Select(c => $"[{c.Tag}] {c.Content}"));

        var chapterOrderSection = string.IsNullOrWhiteSpace(documentChapterOrder)
            ? string.Empty
            : $"\n\nSource document's own chapter order (for reference only — a STRONG " +
              $"SIGNAL for prerequisite/SortOrder decisions, since an earlier chapter " +
              $"usually introduces concepts that are prerequisite to later chapters, but " +
              $"NOT a rigid rule — you still decide the real node structure and " +
              $"dependencies from the actual content, and chapters never become nodes " +
              $"themselves):\n{documentChapterOrder}";

        var prompt =
            $"Capability: {capabilityName}\n\n" +
            $"Curated Corpus Summary:\n{curatedCorpus.Summary}\n\n" +
            $"Curated Corpus Chunks:\n{corpusText}" +
            chapterOrderSection;

        // Run agent with structured output
        var response = await _agent.RunAsync<CapabilityGraphResponse>(prompt, cancellationToken: cancellationToken);

        var graph = response.Result;

        // Ensure required fields are set
        if (graph.CapabilityGraphId == Guid.Empty)
            graph.CapabilityGraphId = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(graph.Name))
            graph.Name = $"{capabilityName} Learning Graph";

        // Resolve edge Source/TargetNodeId from the (LLM-authored) node names.
        // The model never invents GUIDs — it can only reference nodes by their
        // exact Name — so this is the single place where the real NodeId Guids
        // (assigned when each GraphNodeDto was constructed) get wired to edges.
        ResolveEdgeNodeIds(graph);

        // Extract token usage for observability
        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "GraphArchitect",
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        return new ExtractionResult { Graph = graph, TokenUsage = tokenUsage };
    }

    /// <summary>
    /// Resolves each edge's SourceNodeId/TargetNodeId by matching
    /// SourceNodeName/TargetNodeName against the real Node.Name -> NodeId
    /// map. Tries an exact match first, then a case/whitespace-insensitive
    /// match, then falls back to a loose contains-based match so a minor
    /// wording slip from the model never surfaces as "Unknown".
    /// </summary>
    private static void ResolveEdgeNodeIds(CapabilityGraphResponse graph)
    {
        var nodesByExactName = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var nodesByNormalizedName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Nodes)
        {
            var name = node.Name?.Trim() ?? string.Empty;
            if (name.Length == 0) continue;

            nodesByExactName.TryAdd(name, node.NodeId);
            nodesByNormalizedName.TryAdd(name, node.NodeId);
        }

        foreach (var edge in graph.Edges)
        {
            edge.SourceNodeId = ResolveNodeId(edge.SourceNodeName, graph.Nodes, nodesByExactName, nodesByNormalizedName);
            edge.TargetNodeId = ResolveNodeId(edge.TargetNodeName, graph.Nodes, nodesByExactName, nodesByNormalizedName);
        }
    }

    private static Guid ResolveNodeId(
        string? referencedName,
        List<GraphNodeDto> nodes,
        Dictionary<string, Guid> nodesByExactName,
        Dictionary<string, Guid> nodesByNormalizedName)
    {
        if (string.IsNullOrWhiteSpace(referencedName))
            return Guid.Empty;

        var trimmed = referencedName.Trim();

        if (nodesByExactName.TryGetValue(trimmed, out var exactId))
            return exactId;

        if (nodesByNormalizedName.TryGetValue(trimmed, out var normalizedId))
            return normalizedId;

        // Loose fallback: the model referenced a name that doesn't exactly
        // match a node — find the closest node by substring containment
        // rather than giving up and leaving an "Unknown" reference.
        var fuzzy = nodes.FirstOrDefault(n =>
            !string.IsNullOrWhiteSpace(n.Name) &&
            (n.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
             trimmed.Contains(n.Name, StringComparison.OrdinalIgnoreCase)));

        return fuzzy?.NodeId ?? Guid.Empty;
    }
}

/// <summary>
/// Enum for graph node types (pedagogical classification).
/// </summary>
public enum NodeType
{
    Concept = 0,  // Theoretical knowledge
    Skill = 1     // Practical ability
}

/// <summary>
/// Enum for graph edge relationship types.
/// </summary>
public enum EdgeRelationshipType
{
    Requires = 0,   // Prerequisite dependency
    BuildsOn = 1    // Incremental extension
}
