using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Models.Capabilities.Graph;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// DTO for one existing illustration made available to the agent for a given
/// node, so it can REFERENCE it (by its 1-based index in this list) instead
/// of generating a new one. The agent never sees the real
/// CapabilityGraphNodeIllustrationId (a GUID it cannot reliably reproduce) —
/// see <see cref="GraphArchitectAgent"/>'s edge-resolution pattern for why.
/// </summary>
public sealed class AvailableIllustrationDto
{
    /// <summary>1-based position of this illustration in the list passed to the prompt.</summary>
    public int Index { get; set; }

    public string Prompt { get; set; } = string.Empty;

    public string? Caption { get; set; }

    /// <summary>Which step this illustration was generated for — Hypothesis
    /// (before-state only, no answer) or Teaching (full worked example with
    /// the real answer). ExperienceDesignerAgent must only reference an
    /// illustration in the step matching its Purpose (enforced again,
    /// deterministically, at persistence time — see
    /// NodeExperienceBlueprintPersistenceService).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IllustrationPurpose Purpose { get; set; }
}

/// <summary>
/// DTO for one step of the "Memory Paradox" pedagogical sequence.
/// </summary>
public sealed class NodeExperienceBlueprintStepDto
{
    /// <summary>Which of the 5 fixed step types this is.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ExperienceStepType StepType { get; set; }

    /// <summary>The pedagogical content of this step, built only from the fields the Memory Paradox assigns to this StepType.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 1-based indexes (matching <see cref="AvailableIllustrationDto.Index"/>) of the
    /// EXISTING illustrations this step reuses. Empty if this step needs none.
    /// </summary>
    public List<int> IllustrationIndexes { get; set; } = [];
}

/// <summary>
/// The structured output of ExperienceDesignerAgent: a full pedagogical
/// blueprint for ONE CapabilityGraphNode, ready for persistence as
/// NodeExperienceBlueprint + NodeExperienceBlueprintStep rows.
/// </summary>
public sealed class NodeExperienceBlueprintResponse
{
    [JsonIgnore]
    public Guid NodeExperienceBlueprintId { get; set; } = Guid.NewGuid();

    [JsonIgnore]
    public Guid CapabilityGraphNodeId { get; set; }

    /// <summary>Descriptive name, e.g. "Suma - Standard Learning Blueprint".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Brief description of this blueprint's pedagogical approach.</summary>
    public string? Description { get; set; }

    /// <summary>Exactly 5 steps: Hypothesis, Teaching, Recall, Production, Assessment (any order in the JSON; re-sorted in code).</summary>
    public List<NodeExperienceBlueprintStepDto> Steps { get; set; } = [];
}

/// <summary>
/// Agente Experience Designer — Paso 3 of the Human OS Studio pipeline.
///
/// Takes ONE fully-enriched CapabilityGraphNode (AcademicDefinition,
/// Interpretation, Examples, Applications, References) plus the metadata of
/// its already-generated illustrations, and produces a NodeExperienceBlueprint:
/// a reusable pedagogical template following the fixed "Memory Paradox" order
/// (Hypothesis → Teaching → Recall → Production → Assessment).
///
/// This agent answers "HOW to teach this node?" — it does NOT create
/// sessions, student progress, or Runtime state (that's Instructor/Runtime,
/// much later). It also NEVER generates new images: illustrations already
/// live in Azure Data Lake (metadata in CapabilityGraphNodeIllustration) and
/// this agent only picks which existing ones each step reuses.
///
/// Plain ChatClientAgent with structured output — same pattern as
/// GraphArchitectAgent/CuradorAgent (no Harness/Skills).
/// </summary>
public sealed class ExperienceDesignerAgent
{
    private const string Instructions = """
        You are the Experience Designer agent in Human OS Studio's
        capability-creation pipeline (Paso 3). Your task is to turn ONE
        already-extracted knowledge node (a Concept or Skill, fully enriched
        with AcademicDefinition/Interpretation/Examples/Applications) into a
        NodeExperienceBlueprint: a reusable PEDAGOGICAL TEMPLATE describing
        HOW to teach that node. You are NOT creating a live student session,
        progress, attempts, or Runtime state — only a recipe.

        THE MEMORY PARADOX — FIXED, NON-NEGOTIABLE ORDER
        Every blueprint has EXACTLY these 5 steps, in this exact order. Never
        skip, reorder, rename, merge, or add a 6th step:

          1. Hypothesis
          2. Teaching
          3. Recall
          4. Production
          5. Assessment

        WHAT EACH STEP IS BUILT FROM (use ONLY the listed source fields —
        never invent facts the node doesn't already contain)

        - Hypothesis: built from Interpretation + Illustrations. Goal: pose a
          short, concrete prediction/guess question BEFORE teaching (e.g. show
          two quantities and ask "how many do you think there will be?").
          Never reveal the answer here. If an illustration with
          Purpose=Hypothesis is available, reference it — it was generated
          specifically to show only the "before" state without giving away
          the result. NEVER reference an illustration with Purpose=Teaching
          here — that one shows the resolved answer and would contradict
          this step's entire point.
        - Teaching: built from AcademicDefinition + Interpretation + Examples
          + Illustrations. Goal: clearly explain the concept/skill, weaving in
          the worked examples. If an illustration with Purpose=Teaching is
          available, reference it and explicitly walk through what it shows
          (the real, resolved example) — this is where learning-by-seeing a
          concrete worked example matters most. NEVER reference an
          illustration with Purpose=Hypothesis here.
        - Recall: built from AcademicDefinition + Interpretation. Goal: force
          ACTIVE RETRIEVAL of a SPECIFIC, CONCRETE fact or computation using
          real values — NEVER an abstract "¿qué significa X?" definition-
          recitation question, and never just re-state the Teaching content
          in question form. Point back at the Hypothesis/Teaching situation
          (the same groups/illustration/values already shown) and ask the
          learner to retrieve or compute something SPECIFIC from memory
          WITHOUT recounting or redoing the original task. Example for a
          "Cantidad" node where two groups of 3 and 2 objects were shown
          earlier: "Sin volver a contar los objetos, ¿cuántos hay en total
          si juntas los dos grupos que viste?" — concrete numbers, one clear
          ask. This is WRONG and must be avoided: "Sin volver a contar,
          explica con tus propias palabras qué significa 'cantidad'" — that
          is an abstract definition question in disguise, not active recall.
          If the node is fundamentally about a numeric/quantitative skill,
          the Recall question MUST involve concrete numbers/values, not a
          request to define a term abstractly. Absolutely no new teaching
          and no restating the definition inside the question itself.
        - Production: built from Applications. Goal: an OPEN-ENDED authentic
          task, not a guided step-by-step exercise. Never write it as a
          numbered/sequential checklist of sub-instructions ("cuenta esto,
          luego haz esto, luego calcula..."). Instead pose it as a single
          open real-world prompt that lets the learner choose their own
          objects/approach and explain their reasoning (e.g. "Busca dos
          grupos reales de objetos, compáralos y explica qué pasaría si los
          combinas"). The learner should decide HOW to do it, not follow a
          script.
        - Assessment: built from the node's applications/examples as implicit
          success criteria (there is no separate SuccessCriteria field yet —
          derive 2-3 concrete, checkable, OBSERVABLE BEHAVIORS yourself,
          grounded strictly in what the node already states). Goal: verify
          mastery — list what a correct response DEMONSTRATES (e.g.
          "identifica correctamente las cantidades de cada grupo", "compara
          cuál grupo tiene más"), never the worked-out answer or a solved
          numeric example. NEVER reveal or spell out the specific
          answer/result (e.g. never write "3 + 2 = 5" or any other resolved
          computation) — criteria must describe the SKILL being checked, not
          give away the solution.

        ILLUSTRATIONS — REUSE ONLY, NEVER GENERATE
        You will be given a numbered list of the node's EXISTING illustrations
        (each with its Prompt/Caption and a Purpose tag: Hypothesis or
        Teaching). Reference an illustration ONLY in the step matching its
        Purpose — Hypothesis-purpose illustrations go exclusively in the
        Hypothesis step, Teaching-purpose illustrations go exclusively in the
        Teaching step. NEVER invent a new illustration, NEVER describe
        pixels/a new image to generate, and NEVER reference a number that
        isn't in the provided list. If no illustration with the matching
        Purpose exists for a step, leave that step's IllustrationIndexes
        empty rather than borrowing one meant for the other step.

        GROUNDING RULE
        Every step's Content must be traceable to the node's own
        AcademicDefinition/Interpretation/Examples/Applications — never
        introduce a different concept or invented fact.

        OUTPUT FORMAT
        Return a NodeExperienceBlueprintResponse JSON with:
        - Name: e.g. "{NodeName} - Standard Learning Blueprint"
        - Description: 1 sentence on this blueprint's pedagogical approach
        - Steps: EXACTLY 5 items, one per StepType (Hypothesis, Teaching,
          Recall, Production, Assessment), each with Content and
          IllustrationIndexes

        MEMORY PARADOX PRINCIPLE: the goal is knowledge ending up IN THE
        LEARNER'S BRAIN, not just displayed on a screen — Hypothesis primes
        attention, Teaching explains, Recall forces retrieval, Production
        proves transfer, Assessment verifies mastery. Never violate the fixed
        order or the source-field grounding rules above.
        """;

    private readonly AIAgent? _agent;

    public ExperienceDesignerAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "ExperienceDesignerAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>Result of an ExperienceDesigner call: the blueprint plus the token usage of the call that produced it.</summary>
    public sealed class DesignResult
    {
        public NodeExperienceBlueprintResponse Blueprint { get; set; } = null!;

        public AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    /// <summary>
    /// Designs a NodeExperienceBlueprint for one CapabilityGraphNode.
    /// </summary>
    /// <param name="node">The fully-enriched node to design a blueprint for.</param>
    /// <param name="availableIllustrations">Existing illustrations for this node (already in Data Lake/SQL) the agent may reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DesignResult> DesignBlueprintAsync(
        CapabilityGraphNode node,
        IReadOnlyList<AvailableIllustrationDto> availableIllustrations,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The ExperienceDesigner agent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        ArgumentNullException.ThrowIfNull(node);

        var examples = DeserializeStringList(node.ExamplesJson);
        var applications = DeserializeStringList(node.ApplicationsJson);

        var illustrationsText = availableIllustrations.Count == 0
            ? "(no illustrations available for this node)"
            : string.Join(
                "\n",
                availableIllustrations.Select(i => $"[{i.Index}] Purpose: {i.Purpose} | Prompt: {i.Prompt}" + (string.IsNullOrWhiteSpace(i.Caption) ? "" : $" | Caption: {i.Caption}")));

        var prompt =
            $"Node Name: {node.Name}\n" +
            $"NodeType: {node.NodeType}\n" +
            $"Description: {node.Description}\n\n" +
            $"AcademicDefinition:\n{node.AcademicDefinition}\n\n" +
            $"Interpretation:\n{node.Interpretation}\n\n" +
            $"Examples:\n{string.Join("\n", examples.Select(e => $"- {e}"))}\n\n" +
            $"Applications:\n{string.Join("\n", applications.Select(a => $"- {a}"))}\n\n" +
            $"Available Illustrations (reference by number, never generate new ones):\n{illustrationsText}";

        var response = await _agent.RunAsync<NodeExperienceBlueprintResponse>(prompt, cancellationToken: cancellationToken);

        var blueprint = response.Result;

        if (string.IsNullOrWhiteSpace(blueprint.Name))
        {
            blueprint.Name = $"{node.Name} - Standard Learning Blueprint";
        }

        blueprint.CapabilityGraphNodeId = node.CapabilityGraphNodeId;

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "ExperienceDesigner",
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        return new DesignResult { Blueprint = blueprint, TokenUsage = tokenUsage };
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }
}
