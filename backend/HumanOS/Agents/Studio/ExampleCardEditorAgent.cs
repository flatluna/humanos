using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Models.Capabilities.Graph;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>Structured output: one Deep Dive ExampleCard (either an edited
/// existing card or a brand-new one).</summary>
public sealed class EditExampleCardResponse
{
    /// <summary>Short card title, same style as ExampleCard.Title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Card body, same restricted HTML tag set as the rest of the
    /// blueprint (p, br, strong, em, ul, ol, li, a).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Emoji(s) illustrating the card, or null if none genuinely fits.</summary>
    public string? Emoji { get; set; }

    /// <summary>Same "leave null rather than invent one" rule as
    /// BlueprintStepEditorAgent — a text prompt for a NEW illustration for
    /// this ONE card, or null when neither the instruction nor the card's
    /// own content calls for one.</summary>
    public string? DiagramPrompt { get; set; }

    /// <summary>Exact numeric spec for a coordinate-plane/chart diagram, set
    /// INSTEAD OF DiagramPrompt when applicable (never both) — same rules
    /// as BlueprintStepEditorAgent.MathDiagram.</summary>
    public MathDiagramSpec? MathDiagram { get; set; }
}

/// <summary>
/// ExampleCardEditorAgent (2026-07-30) — Capability Studio review feature:
/// lets a human reviewer edit ONE ExampleCard of a node's optional Deep
/// Dive step via a free-text instruction, or create a brand-new card to
/// append to the step, WITHOUT ever touching any of the step's other
/// cards. This exists because the pre-existing "edit whole step" flow
/// (BlueprintStepEditorAgent + EditNodeBlueprintStepFunction) only knows
/// about a step's plain Content HTML — it has no concept of ExampleCards
/// at all, so using it on the Deep Dive step silently ignored every card
/// (a real incident: asking to "improve the card with sound Zz, add an
/// image" via that generic box appeared to wipe out all 20 cards from the
/// reviewer's screen, because the response it merged back in never
/// echoed ExampleCards at all). This agent operates card-by-card instead,
/// same "plain ChatClientAgent + structured output" pattern as every
/// other Studio agent.
/// </summary>
public sealed class ExampleCardEditorAgent
{
    private const string Instructions = """
        You are the Deep Dive Card Editor inside Human OS Studio's review
        tools. A human reviewer is looking at ONE ExampleCard of an already-
        generated Deep Dive step's coverage pool for one Capability node,
        and either wants to EDIT that one existing card via a free-text
        instruction, or wants a BRAND-NEW card added to the pool. Your ONLY
        job is to produce that ONE card's Title/Text/Emoji (and, if
        warranted, an illustration) — you never see or touch any other
        card, and the reviewer tool guarantees every other card is left
        completely untouched no matter what you return.

        You will receive: the node's full context (AcademicDefinition,
        Interpretation, Examples, Applications), the Titles of every OTHER
        existing card in this step's pool (for context and to avoid an
        accidental duplicate), whether this is an EDIT (with the card's
        CURRENT Title/Text/Emoji and whether it currently has an
        illustration) or an ADD (no current card — you are inventing a new
        one), and the reviewer's instruction.

        RULES:
        - EDIT mode: honor the reviewer's instruction as literally and
          completely as possible while keeping the card's underlying
          concept intact, unless the instruction explicitly asks to change
          the example itself (e.g. "usa un ejemplo de animales en vez de
          frutas") — in that case the new example may use different
          concrete objects/scenarios, but it must still teach the SAME
          skill/concept the Deep Dive step as a whole is covering.
        - ADD mode: invent a new card that teaches a genuinely useful,
          non-duplicate angle on the node's own content (grounded in
          AcademicDefinition/Interpretation/Examples/Applications) that
          isn't already covered by one of the other existing card Titles
          you were given. Follow the reviewer's instruction for what this
          new card should focus on.
        - Never invent a fact the node doesn't support.
        - CONTENT FORMAT: same simple semantic HTML as the rest of the
          blueprint. Only these tags are ever allowed: p, br, strong, em,
          ul, ol, li, a. Never headings, tables, images, scripts, or inline
          styles.
        - Title stays short (a few words), matching the style of the other
          existing card Titles you were given.
        - LANGUAGE: if the prompt gives you a REQUIRED RESPONSE LANGUAGE,
          write Title and Text in EXACTLY that language, even if the
          reviewer's own free-text instruction is written in a different
          language — never switch the course's content language just
          because the instruction itself was typed in another language.
          Only when no such directive is given, write in the same language
          as the other existing card Titles you were given.

        DECIDING WHETHER TO ADD/REPLACE THE ILLUSTRATION (DiagramPrompt)
        Propose one (fill in DiagramPrompt) in ANY of these three cases:
          1. EXPLICIT REQUEST — the instruction is clearly about the image
             itself (e.g. "agrega una imagen", "cambia la ilustración",
             "ponle un dibujo de un perrito").
          2. CONTRADICTION — editing the card changed the underlying
             scenario/example enough that the OLD illustration (if any)
             would now contradict the new text.
          3. AUTOMATIC/PROACTIVE — the card currently has no illustration
             at all (or, for ADD mode, none was requested), but the
             (possibly just-written) Text describes a genuinely concrete,
             visualizable scenario (countable objects, comparable groups, a
             real-world process) where a learner would clearly benefit from
             seeing it, not just reading it.
        Describe the new illustration's scene in DiagramPrompt, revealing
        the resolved/worked example (Deep Dive cards are worked examples,
        never a withheld-answer Hypothesis-style scene). Image generation
        models don't calculate — if the scene includes any number/quantity,
        write the exact value explicitly. If none of the 3 cases apply,
        leave DiagramPrompt null.

        MathDiagram VS DiagramPrompt
        Whenever one of the 3 cases above applies AND the illustration is
        literally a coordinate-plane graph of a real equation (a circle, an
        ellipse (a≠b — use circle when a=b), a line, a parabola, or a set of
        exact plotted points) or a statistics/chart visualization (bar
        chart, histogram, pie chart, line chart, scatter plot, number
        line), fill MathDiagram with those EXACT numbers instead of
        DiagramPrompt (never both) — a generative image model cannot
        reliably place a labeled coordinate/bar/slice at the exact right
        spot, this renders deterministically (SkiaSharp) instead. For every
        other concrete scene, keep using DiagramPrompt.
        """;

    private readonly AIAgent? _agent;

    public ExampleCardEditorAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        // Economy tier: this only applies ONE bounded, explicit reviewer
        // instruction to ONE card (never designs a whole blueprint from
        // scratch), a good candidate for a cheaper model. Falls back to the
        // main deployment if 'AzureOpenAIEconomyDeploymentName' isn't set.
        var deploymentName = configuration["AzureOpenAIEconomyDeploymentName"] ?? configuration["AzureOpenAIDeploymentName"];
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
            .AsAIAgent(instructions: Instructions, name: "ExampleCardEditorAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<EditExampleCardResponse> EditCardAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "ExampleCardEditorAgent is not configured. Set the 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName' application settings.");
        }

        var response = await _agent.RunAsync<EditExampleCardResponse>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }
}
