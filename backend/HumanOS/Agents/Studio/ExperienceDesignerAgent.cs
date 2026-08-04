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
/// DTO for one "example card" of the OPTIONAL Deep Dive step. See
/// <see cref="HumanOS.Models.Capabilities.Graph.ExampleCard"/> for the
/// persisted shape this maps 1:1 onto.
/// </summary>
public sealed class ExampleCardDto
{
    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string? Emoji { get; set; }

    /// <summary>
    /// True when the agent judges this specific card is worth a real,
    /// generated illustration (not just its Emoji) — see the "COVERAGE
    /// POOL" rules below for how many cards should be marked true.
    /// <see cref="Services.ExampleCardIllustrationService"/> only generates
    /// a real image for cards where this is true (subject to its own hard
    /// cost cap); every other card falls back to Emoji-only.
    /// </summary>
    public bool SuggestedForIllustration { get; set; }

    /// <summary>
    /// NOT part of the agent's structured-output schema (the model never
    /// invents this) — set in code AFTER the agent responds, by
    /// <see cref="Services.ExampleCardIllustrationService"/>, once a real
    /// illustration has been generated for this card. Same "resolved in
    /// code after the response comes back" pattern as
    /// <see cref="GraphEdgeDto.SourceNodeId"/>.
    /// </summary>
    [JsonIgnore]
    public Guid? IllustrationId { get; set; }
}

/// <summary>
/// DTO for one step of the "Memory Paradox" pedagogical sequence.
/// </summary>
public sealed class NodeExperienceBlueprintStepDto
{
    /// <summary>Which of the 5 fixed step types this is (or the optional 6th, DeepDive).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ExperienceStepType StepType { get; set; }

    /// <summary>The pedagogical content of this step, built only from the fields the Memory Paradox assigns to this StepType.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 1-based indexes (matching <see cref="AvailableIllustrationDto.Index"/>) of the
    /// EXISTING illustrations this step reuses. Empty if this step needs none.
    /// </summary>
    public List<int> IllustrationIndexes { get; set; } = [];

    /// <summary>
    /// ONLY populated when StepType == DeepDive: 3+ short example cards
    /// (Title/Text/Emoji). Null/empty for every other step type.
    /// </summary>
    public List<ExampleCardDto>? ExampleCards { get; set; }
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
  private const int MaxDeepDiveCardsPerNodeHardLimit = 10;

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
          ACTIVE RETRIEVAL of a SPECIFIC, CONCRETE fact or computation —
          NEVER an abstract "¿qué significa X?" definition-recitation
          question, and never just re-state the Teaching content in question
          form. Absolutely no new teaching and no restating the definition
          inside the question itself.
          CRITICAL DISTINCTION (fixed 2026-07-20 — real production bug: a
          "Language Models" node's Recall asked "¿cuáles eran las tres
          palabras conectadas que se usaron para mostrar cómo un modelo de
          lenguaje aprende relaciones semánticas?" and a "Resumir texto" node
          asked "¿cuántas frases tenía el resumen mostrado junto al
          documento?" — both test a trivial, INCIDENTAL detail of how the
          Teaching illustration/example happened to be presented, not the
          actual capability):
            (a) If the node is a NUMERIC/QUANTITATIVE/PROCEDURAL skill (the
                concrete numbers/values ARE the substance of what's being
                learned, e.g. arithmetic, measurement, counting): point back
                at the Hypothesis/Teaching situation (the same groups/
                illustration/values already shown) and ask the learner to
                retrieve or compute something SPECIFIC from memory WITHOUT
                recounting or redoing the original task. Example for a
                "Cantidad" node where two groups of 3 and 2 objects were
                shown earlier: "Sin volver a contar los objetos, ¿cuántos hay
                en total si juntas los dos grupos que viste?"
            (b) If the node is a CONCEPTUAL/DEFINITIONAL skill (the
                illustration merely used SOME arbitrary example — specific
                words, a specific sentence count, specific labels — to
                demonstrate a general idea/mechanism, and a DIFFERENT example
                would have worked just as well): NEVER ask the learner to
                recall the illustration's own incidental specifics (the
                exact words/items/count/labels it happened to show). Instead
                ask them to apply or identify the underlying mechanism/
                criterion itself, e.g. with a NEW instance they must reason
                about, or by asking what the general pattern/rule was. Litmus
                test: if the learner mastered the CAPABILITY but simply forgot
                this one incidental detail of the example, would they still
                deserve full credit? If yes, that detail is the wrong thing
                to ask about — write a different Recall question. This is
                WRONG and must be avoided just as much as the abstract-
                definition question above: "Sin volver a mirar la
                ilustración, ¿cuáles eran las tres palabras que aparecían
                conectadas?" — that is illustration-presentation trivia in
                disguise, not active recall of the capability.
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

        PRESERVE VERBATIM IDENTIFIERS FROM THE SOURCE FIELDS
        If AcademicDefinition or Examples names a specific, citable
        identifier — a legal/regulatory article or section number (e.g.
        "ARTÍCULO 44"), a named entity (company/organization/person), a
        date, or a monetary amount — the Teaching step's Content MUST state
        that identifier explicitly, not just paraphrase the general idea
        around it. A learner reading Teaching should be able to see exactly
        which article/entity is being explained, not only a generic
        description of the rule. Never smooth away a specific citation
        into vague prose ("this rule covers an exemption...") when the
        source field already names precisely which article/entity it is.

        CONTENT FORMAT — SIMPLE SEMANTIC HTML, NOT A PLAIN-TEXT WALL
        Each step's Content is rendered directly as sanitized HTML to the
        student (not as raw/plain text), so use a SMALL set of semantic
        tags whenever it genuinely improves readability — never as
        decoration for its own sake:
        - <p>...</p> to separate distinct ideas/paragraphs.
        - <strong>...</strong> to highlight key terms/definitions the
          first time they appear.
        - <ul><li>...</li></ul> or <ol><li>...</li></ol> for lists of
          examples, steps, or assessment criteria (Assessment's 2-3
          observable-behavior criteria are ALWAYS a <ul>, never a single
          paragraph).
        - <em>...</em> only for genuine emphasis, sparingly.
        Only these tags are ever allowed: p, br, strong, em, ul, ol, li, a.
        Never use headings, tables, images, scripts, inline styles, or any
        other tag — the renderer strips anything else. If the node's own
        material contains a source citation written as Markdown
        "[Title](Url)" (e.g. from a Grounding-with-Bing-Search finding),
        convert it into a real HTML link: <a href="Url">Title</a> — never
        leave the raw "[Title](Url)" markdown syntax in the output HTML.

        OPTIONAL "Current Web Findings" INPUT (Grounding-with-Bing-Search)
        Some requests include an extra "Current Web Findings" section —
        real-time web search results fetched specifically for this node
        because it was flagged as covering something that evolves over
        time. Treat it as a SUPPLEMENTARY, OPTIONAL source, never a
        replacement for AcademicDefinition/Interpretation/Examples/
        Applications:
        - Use it ONLY where it genuinely adds current, relevant value to
          Teaching (most common) or Interpretation-driven steps — e.g. a
          concrete recent example, a current tool/version, an up-to-date
          statistic. Never force it in if it doesn't clearly fit.
        - Every fact you take from it MUST keep its inline "[Title](Url)"
          citation, converted to a real HTML link as described above —
          never state a fact from this section without its citation, and
          never drop the citation while paraphrasing the surrounding
          prose.
        - If the findings don't say anything useful/relevant to this
          specific node, ignore them entirely rather than padding content
          with unrelated trivia.
        - If this section instead contains a bracketed NOTE saying no
          verified web findings were available for a node flagged as
          needing current info, treat that as an explicit instruction to be
          conservative: avoid stating specific recent facts/details you
          cannot verify, and prefer general, timeless framing over a
          specific claim you're not sure is still accurate.

        OUTPUT FORMAT
        Return a NodeExperienceBlueprintResponse JSON with:
        - Name: e.g. "{NodeName} - Standard Learning Blueprint"
        - Description: 1 sentence on this blueprint's pedagogical approach
        - Steps: EXACTLY 5 items, one per StepType (Hypothesis, Teaching,
          Recall, Production, Assessment), each with Content (HTML, per the
          format rules above) and IllustrationIndexes

        MEMORY PARADOX PRINCIPLE: the goal is knowledge ending up IN THE
        LEARNER'S BRAIN, not just displayed on a screen — Hypothesis primes
        attention, Teaching explains, Recall forces retrieval, Production
        proves transfer, Assessment verifies mastery. Never violate the fixed
        order or the source-field grounding rules above.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;

    /// <summary>
    /// Persona block appended to the prompt ONLY when the owning Capability's
    /// PedagogicalAudience is "YoungLearner" (early-childhood/K-12 courses,
    /// e.g. a Grade 1 phonics/reading course). Does NOT change the fixed
    /// 5-step structure or the source-field grounding rules above — it only
    /// changes the VOICE/TONE the Content is written in.
    /// </summary>
    private const string YoungLearnerPersonaBlock = """
        AUDIENCE PERSONA — YOUNG LEARNER MODE (this capability is for young
        children, roughly ages 5-8, e.g. Kindergarten/Grade 1)
        Write every step's Content as a warm, friendly, patient, playful, and
        encouraging teacher speaking DIRECTLY to the child — never to a parent
        or teacher, and never in a neutral/academic register. Concretely:
        - Address the child as "you", in simple words a young child already
          knows. Never assume prior vocabulary or reading fluency beyond what
          the node itself establishes.
        - Use short sentences and short paragraphs. Avoid abstract or formal
          phrasing — explain everything as if talking out loud to the child.
        - Sound genuinely excited and encouraging (e.g. "Great job trying!",
          "You're doing amazing!", "Let's go!") — never scold or make a
          mistake feel bad; frame mistakes as a normal part of learning.
        - Hypothesis: open with a short, friendly greeting/warm-up before the
          guess question (e.g. "Hi again! Today we're going to look at
          something fun.") — still never reveal the answer.
        - Teaching: explain like a caring teacher sitting beside the child,
          walking through the example step by step in simple words.
        - Recall/Production: phrase practice prompts like a game or a fun
          challenge, not a test.
        - Assessment: keep the required observable-behavior criteria (for the
          internal mastery check), but the surrounding Content should still
          read like a friendly wrap-up, and may include a short celebratory
          closing line once criteria are listed.
        This persona changes ONLY the voice/tone — it never changes which of
        the 5 fixed steps exists, their order, or which source fields ground
        each step's Content.
        """;

    /// <summary>
    /// Prompt addendum appended ONLY when the owning Capability has
    /// IncludeExamplesStep=true (formal/school-track capabilities).
    /// Describes the OPTIONAL 6th step, Deep Dive, without altering the 5
    /// fixed steps' rules above.
    /// </summary>
    private const string ExamplesStepAddendum = """
        THIS CAPABILITY ALSO REQUIRES AN OPTIONAL 6TH STEP: DEEP DIVE
        In addition to the 5 fixed steps above, produce ONE more step with
        StepType="DeepDive", conceptually positioned between Teaching and
        Recall (final order: Hypothesis, Teaching, DeepDive, Recall,
        Production, Assessment — you may still return the 6 steps in any
        JSON order, code re-sorts them). At runtime, Deep Dive is an
        INTERACTIVE mastery loop (not passive reading): the student is
        quizzed on each card, one at a time, strictly in the order you
        return them, and must demonstrate mastery of EACH card before
        moving to the next — so the cards you generate ARE the quizzable
        content, not just illustrative reading material.
        COVERAGE POOL — GENERATE THE FULL SCOPE, NOT A TOKEN SAMPLE
        A learner must reach REAL mastery of this node's full scope, not
        just superficial familiarity with 2-3 arbitrary instances. For
        EVERY node, first decide: "What is the complete set of concrete
        instances a learner needs to practice to truly master this
        concept/skill?" This reasoning is domain-agnostic — apply it
        equally to reading, math, cooking, programming, or any other
        subject:
        - If the concept/skill has a NATURAL, FINITE, ENUMERABLE scope
          (e.g. all 26 letters of the alphabet and their sounds, the full
          set of single-digit addition facts, the days of the week, the
          countries of a region, the basic knife cuts in cooking), generate
          ONE card per item in that ENTIRE set — never an arbitrary small
          subset of a finite domain.
        - If the concept/skill is OPEN-ENDED/unbounded (e.g. reading
          comprehension of varied sentences, general conversation practice,
          creative writing prompts), generate a large REPRESENTATIVE set
          (aim for 15-30 cards) that meaningfully covers the range of ways
          the concept/skill shows up — never just 2-3 token examples.
        - Never fall back to a tiny fixed count "for simplicity" — a course
          this thin does not achieve real learning, which defeats the whole
          purpose of this step existing.
        Every card must still be grounded strictly in this node's own
        AcademicDefinition/Interpretation/Examples/Applications (never an
        invented fact the node doesn't already contain), and each card must
        show something genuinely DIFFERENT — never near-duplicates or mere
        rewordings of the same instance.

        EACH CARD:
        - Title: a short concrete label, e.g. "Letter Aa" or "Rhyming Words".
        - Text: 2-4 short sentences using ONLY the restricted HTML tags
          (p, br, strong, em, ul, ol, li, a) that concretely illustrate ONE
          specific, grounded instance.
          CRITICAL — ALWAYS STATE THE RESOLVED RESULT (fixed 2026-07-29):
          these are WORKED EXAMPLES for the student to learn FROM, never
          unsolved exercises for the student to solve. Text must ALWAYS
          state the concrete outcome/result/answer explicitly and plainly
          — NEVER end on an open, unanswered question like "¿cuántos hay en
          total?" or "how many are there?" without immediately giving the
          resolved number/answer in the same Text. If posing a question
          helps engagement, it must be immediately followed, in the same
          Text, by its resolved answer. Good example: "Un niño tiene 2
          carros y después agrega 3 más. Al juntar todos los carros, en
          total tiene 5 carros." Bad example (missing the result): "Un niño
          tiene 2 carros y después agrega 3 más. ¿Cuántos carros tiene en
          total?" — never write a card like this second one.
        - Emoji: OPTIONAL 1-4 emoji characters that visually represent the
          card's concrete content (e.g. "🍎" for an apple example, "🐱🎩" for
          a cat-wearing-a-hat rhyme example). Omit (null) if no emoji
          genuinely fits — never force an unrelated emoji.
        - SuggestedForIllustration: mark TRUE for a small, carefully-chosen
          SUBSET of cards that best represent the full range of the pool
          (a good default target is 5-8 cards, but use judgment: if the
          whole pool is smaller than that, mark all of them true; if the
          pool is large, keep the illustrated subset proportionally small —
          real generated images are expensive, so this subset must stay
          deliberately curated, never all of them). Mark FALSE for every
          other card — those still teach fully via Title/Text/Emoji, just
          without a generated image.

        Also fill this step's own Content field with a short (1-2 sentence)
        HTML introduction framing what the cards below are about — Content
        must never be empty, even though the real substance lives in
        ExampleCards.
        This step never uses IllustrationIndexes (leave it empty) — cards
        marked SuggestedForIllustration=true get a real generated
        illustration assigned by code after your response; every other
        card's illustration is its own Emoji field.
        """;

    public ExperienceDesignerAgent(IConfiguration configuration)
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
    /// <param name="webFindings">
    /// Optional real-time Grounding-with-Bing-Search findings fetched
    /// specifically for this node (only ever populated when the node was
    /// flagged NeedsCurrentInfo=true by GraphArchitectAgent). Null/empty
    /// when no web enrichment was performed for this node.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="languageCode">
    /// "es" or "en" — the language ALL authored blueprint step Content
    /// must be written in. Defaults to "es" for backward compatibility.
    /// See /memories/repo (bilingual course-creation feature, 2026-07-26).
    /// </param>
    /// <param name="pedagogicalAudience">
    /// Optional tone/persona directive, copied straight from the owning
    /// Capability's <see cref="HumanOS.Models.Capabilities.Capability.PedagogicalAudience"/>.
    /// Null/empty (the default) leaves the prompt byte-for-byte identical to
    /// before this parameter existed. "YoungLearner" appends a warm/playful,
    /// direct-to-the-child persona block — see /memories/repo (K-12 lesson
    /// course feature, 2026-07-26).
    /// </param>
    /// <param name="includeExamplesStep">
    /// Copied from the owning Capability's
    /// <see cref="HumanOS.Models.Capabilities.Capability.IncludeExamplesStep"/>.
    /// False (the default) leaves the prompt byte-for-byte identical to
    /// before this parameter existed. True requests an OPTIONAL 6th step,
    /// Examples, with 3+ example cards (2026-07-29 feature).
    /// </param>
    public async Task<DesignResult> DesignBlueprintAsync(
        CapabilityGraphNode node,
        IReadOnlyList<AvailableIllustrationDto> availableIllustrations,
        string? webFindings = null,
        string languageCode = "es",
        string? pedagogicalAudience = null,
        bool includeExamplesStep = true,
      int? maxDeepDiveCardsPerNode = null,
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

        var webFindingsSection = string.IsNullOrWhiteSpace(webFindings)
            ? string.Empty
            : $"\n\nCurrent Web Findings (Bing Grounding — supplementary/optional, see Instructions):\n{webFindings}";

        var personaSection = string.Equals(pedagogicalAudience, "YoungLearner", StringComparison.OrdinalIgnoreCase)
            ? "\n\n" + YoungLearnerPersonaBlock
            : string.Empty;

        var examplesStepSection = includeExamplesStep
            ? "\n\n" + ExamplesStepAddendum
            : string.Empty;

        var normalizedMaxDeepDiveCardsPerNode = maxDeepDiveCardsPerNode.HasValue
          ? Math.Clamp(maxDeepDiveCardsPerNode.Value, 1, MaxDeepDiveCardsPerNodeHardLimit)
          : (int?)null;

        var maxCardsSection = includeExamplesStep && normalizedMaxDeepDiveCardsPerNode.HasValue
          ? $"\n\nHARD LIMIT FOR THIS COURSE: generate at most {normalizedMaxDeepDiveCardsPerNode.Value} Deep Dive cards for this node. Never return more than this limit."
          : string.Empty;

        var prompt =
            $"Node Name: {node.Name}\n" +
            $"NodeType: {node.NodeType}\n" +
            $"Description: {node.Description}\n\n" +
            $"AcademicDefinition:\n{node.AcademicDefinition}\n\n" +
            $"Interpretation:\n{node.Interpretation}\n\n" +
            $"Examples:\n{string.Join("\n", examples.Select(e => $"- {e}"))}\n\n" +
            $"Applications:\n{string.Join("\n", applications.Select(a => $"- {a}"))}\n\n" +
            $"Available Illustrations (reference by number, never generate new ones):\n{illustrationsText}" +
            webFindingsSection +
            personaSection +
            examplesStepSection +
            maxCardsSection +
            $"\n\nTarget output language: {(languageCode.Trim().ToLowerInvariant() == "en" ? "English" : "Spanish")}. " +
            "Write every step's Content field in this language, regardless of what language the " +
            "node context above is written in.";

        var response = await _agent.RunAsync<NodeExperienceBlueprintResponse>(prompt, cancellationToken: cancellationToken);

        var blueprint = response.Result;

        if (string.IsNullOrWhiteSpace(blueprint.Name))
        {
            blueprint.Name = $"{node.Name} - Standard Learning Blueprint";
        }

        if (includeExamplesStep && normalizedMaxDeepDiveCardsPerNode is int maxCards)
        {
          var deepDiveStep = blueprint.Steps.FirstOrDefault(s => s.StepType == ExperienceStepType.DeepDive);
          if (deepDiveStep?.ExampleCards is { Count: > 0 } cards && cards.Count > maxCards)
          {
            deepDiveStep.ExampleCards = cards.Take(maxCards).ToList();
          }
        }

        blueprint.CapabilityGraphNodeId = node.CapabilityGraphNodeId;

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "ExperienceDesigner",
            ModelName = _modelName,
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
