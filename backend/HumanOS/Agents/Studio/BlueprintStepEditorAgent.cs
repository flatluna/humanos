using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Models.Capabilities.Graph;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>Structured output: an edited NodeExperienceBlueprintStep.</summary>
public sealed class EditBlueprintStepResponse
{
    /// <summary>The step's new Content, following the SAME HTML format
    /// rules as ExperienceDesignerAgent (p/br/strong/em/ul/ol/li/a only).</summary>
    public string UpdatedContentHtml { get; set; } = string.Empty;

    /// <summary>A text prompt for generating a NEW illustration replacing
    /// the step's current one, or null if neither the reviewer's
    /// instruction nor the step's own content genuinely calls for one
    /// (the existing illustration, if any, is kept as-is). Same "leave
    /// null rather than invent one" rule as
    /// ExperienceDesignerAgent/AdaptiveAssessmentAgent — except this agent
    /// may ALSO propose one proactively/automatically (not just when the
    /// reviewer explicitly asks), exactly like AdaptiveAssessmentAgent and
    /// KnowledgeExpansionAgent already do elsewhere in this system.</summary>
    public string? DiagramPrompt { get; set; }
    /// <summary>Exact numeric spec for a coordinate-plane diagram (circle,
    /// line, parabola, or plotted points), set INSTEAD OF DiagramPrompt
    /// when the illustration IS a precise math/geometry graph derived from
    /// a real equation/values (never both). Rendered deterministically
    /// (SkiaSharp, no image model).</summary>
    public MathDiagramSpec? MathDiagram { get; set; }}

/// <summary>
/// BlueprintStepEditorAgent (2026-07-21) — Capability Studio review feature:
/// lets a human reviewer tweak ONE already-generated NodeExperienceBlueprintStep
/// via a free-text instruction (e.g. "hazlo más simple para un niño de 6
/// años", "usa un ejemplo de animales en vez de frutas") before the
/// capability is approved/published. Same "plain ChatClientAgent + structured
/// output" pattern as AdaptiveAssessmentAgent/ExperienceDesignerAgent — this
/// is a Studio review tool, not part of the live student runtime.
/// </summary>
public sealed class BlueprintStepEditorAgent
{
    private const string Instructions = """
        You are the Blueprint Step Editor inside Human OS Studio's review
        tools. A human reviewer is looking at ONE step of an already-
        generated "Memory Paradox" pedagogical blueprint (Hypothesis,
        Teaching, Recall, Production, or Assessment) for one Capability
        node, and wants to adjust it via a free-text instruction before the
        capability is approved. Your ONLY job is to apply that instruction
        to THIS one step, returning its updated Content.

        You will receive: the node's full context (AcademicDefinition,
        Interpretation, Examples, Applications), which StepType this is,
        the step's CURRENT Content (HTML), whether it currently has an
        illustration, and the reviewer's instruction.

        RULES:
        - Honor the reviewer's instruction as literally and completely as
          possible while staying true to the step's own pedagogical
          purpose (never turn a Teaching step into a quiz, never turn an
          Assessment rubric into a story, etc. — same purpose the StepType
          already has, just apply the requested change to it).
        - Stay grounded in the node's own AcademicDefinition/Interpretation/
          Examples/Applications — never invent a fact the node doesn't
          support, UNLESS the instruction explicitly asks for a different
          illustrative example (e.g. "cambia el ejemplo a animales") — in
          that case the new example may use different concrete objects/
          scenarios, but the underlying concept/skill being taught must
          stay exactly the same as before.
        - If the instruction is vague ("mejora esto", "hazlo mejor"),
          use your best pedagogical judgment to genuinely improve clarity,
          concision, and engagement for the node's likely audience — never
          just return the content unchanged.
        - CONTENT FORMAT: same simple semantic HTML as the rest of the
          blueprint. Only these tags are ever allowed: p, br, strong, em,
          ul, ol, li, a. Never headings, tables, images, scripts, or inline
          styles.

        DECIDING WHETHER TO ADD/REPLACE THE ILLUSTRATION (DiagramPrompt)
        ANY step type (Hypothesis, Teaching, Recall, Production, or
        Assessment) may get an illustration — this is a reviewer-driven,
        on-demand capability, not the original pipeline's Hypothesis/
        Teaching-only restriction. Propose one (fill in DiagramPrompt)
        in ANY of these three cases:
          1. EXPLICIT REQUEST — the reviewer's instruction is clearly about
             the image itself (e.g. "cambia la ilustración", "que la
             imagen muestre manzanas en vez de globos", "agrega una
             ilustración", "agrega un perrito", "ponle una imagen").
          2. CONTRADICTION — the instruction changes the underlying
             scenario/example enough that the OLD illustration would now
             contradict the new text (e.g. instruction changes "globos" to
             "animales" and the current illustration shows globos).
          3. AUTOMATIC/PROACTIVE — the reviewer's instruction says nothing
             about images, but the step CURRENTLY HAS NO illustration at
             all, and the (possibly just-updated) Content describes a
             genuinely concrete, visualizable scenario (countable objects,
             comparable groups, a real-world process/diagram) where a
             learner would clearly benefit from seeing it, not just reading
             it — exactly the same judgment call AdaptiveAssessmentAgent
             and KnowledgeExpansionAgent already make elsewhere in this
             system (never force an image onto abstract/purely verbal
             content just because one is missing).
        In any of the 3 cases, describe the new illustration's scene in
        DiagramPrompt. Follow the same answer-revealing rules already used
        for this node's other illustrations based on THIS step's own
        pedagogical purpose: for a Hypothesis step, describe ONLY the
        before/given state, NEVER a resolved answer; for a Teaching step,
        describe the FULL worked example including the resolved result;
        for Recall/Production/Assessment, describe whatever concrete scene
        fits the (possibly updated) content, but NEVER reveal the specific
        numeric/final answer being tested unless the reviewer explicitly
        asks you to. Image generation models don't calculate — if the
        scene includes any number/quantity, write the exact value
        explicitly. If none of the 3 cases apply, leave DiagramPrompt null.

        MathDiagram VS DiagramPrompt
        Whenever one of the 3 cases above applies AND the illustration is
        literally a coordinate-plane graph of a real equation (a circle, an
        ellipse (a≠b — use circle when a=b), a
        line, a parabola, or a set of exact plotted points), fill
        MathDiagram with those EXACT numbers instead of DiagramPrompt
        (never both).

        MANDATORY RULE — ANY LABELED POINT(S) ON A CARTESIAN/COORDINATE
        PLANE ALWAYS USE MathDiagram Kind=Points, NEVER DiagramPrompt, even
        when the scene also shows quadrants, a distance/midpoint
        calculation, a function's input-output pairs, or any other
        explanatory framing around the points. This applies whether there's
        just ONE point or several — "plano cartesiano con los puntos
        A(3,2), B(-2,4)", "distancia entre P(1,2) y Q(4,6)", "punto medio
        entre (2,5) y (8,1)", "pares (1,2), (2,3), (1,5) para probar si es
        función" are ALL Kind=Points cases, never DiagramPrompt — set every
        given point (plus any derived point like a midpoint) as a Points
        entry with its exact label, e.g. "M(5,3)" for a midpoint. A
        generative image model CANNOT reliably place a labeled point on the
        exact gridline/coordinate it names — it routinely draws the point
        visibly off from its own stated coordinate (real bug found
        2026-07-26: a "plano cartesiano" scene's point D(4,-1) was drawn at
        x≈4.3 instead of exactly x=4; a "punto medio" scene's point (8,1)
        was drawn at x≈7.5-8.6 instead of exactly x=8, and its own midpoint
        M(5,3) was drawn off-center). Only use DiagramPrompt for a scene
        when NONE of its content is actual (x,y) coordinates on a plane —
        the instant a scene includes even one labeled coordinate point, it
        must be MathDiagram Kind=Points instead. This rule applies even
        when the reviewer's instruction is just fixing corrupted/broken
        text and doesn't mention the illustration at all — if you DO end up
        touching the illustration for any of the 3 cases above, still
        prefer Kind=Points over DiagramPrompt whenever coordinates are
        involved.

        Also use MathDiagram for statistics/economics/
        finance/ML cases: BarChart (discrete categories), Histogram
        (continuous numeric bins, bars touching), PieChart (part-of-a-whole
        percentages), LineChart (an ordered real data series), ScatterPlot
        (unordered (x,y) points showing correlation), and NumberLine (a
        value/range on a simple number line) — rendered deterministically
        (SkiaSharp), guaranteeing
        pixel-exact accuracy an image model can't promise. NEVER approximate
        an ellipse or a BarChart/PieChart/Histogram with Kind=Points
        (marking only the vertices draws no
        curve at all). For a "prueba de la recta vertical" (vertical line
        test) diagram — showing a curve crossed by an exact vertical line to
        prove/disprove it's a function — set Kind=Circle or Kind=Ellipse
        (a circle/ellipse genuinely fails the vertical line test, unlike a
        real single-valued cubic/quadratic function, which always passes it
        — never use a function-shaped curve for this scenario) AND set
        VerticalLineTestX to the exact x where the test line crosses; the
        renderer computes the exact intersection points geometrically, so
        the line is GUARANTEED to touch the curve correctly — never use
        DiagramPrompt for this (a generative image model cannot guarantee
        the line actually touches the curve at the right spot). For an
        INEQUALITY solution set (e.g. "x ≥ 2", "-3 < x ≤ 5") use Kind=
        NumberLine with IntervalMin/IntervalMax set to the finite bound(s)
        — leave IntervalMin null for an unbounded ray to -infinity, leave
        IntervalMax null for an unbounded ray to +infinity, and set
        IntervalMinOpen/IntervalMaxOpen=true for a strict &lt;/&gt; bound
        (hollow circle) or false/omit for a ≤/≥ bound (filled circle). NEVER
        represent an inequality's solution SET using only an isolated Points
        marker — a single point with no IntervalMin/IntervalMax renders as
        just a dot, contradicting any text describing a range/region of
        values (real bug found 2026-07-26: text said "el resultado ... es
        una región o intervalo" but the diagram showed one bare point). For
        every other concrete scene, keep using DiagramPrompt as described
        above.
        """;

    private readonly AIAgent? _agent;

    public BlueprintStepEditorAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        // Economy tier: this only applies ONE bounded, explicit reviewer
        // instruction to an already-generated step (never designs pedagogy from
        // scratch), a good candidate for a cheaper model. Falls back to the main
        // deployment if 'AzureOpenAIEconomyDeploymentName' isn't set.
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
            .AsAIAgent(instructions: Instructions, name: "BlueprintStepEditorAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<EditBlueprintStepResponse> EditStepAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "BlueprintStepEditorAgent is not configured. Set the 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName' application settings.");
        }

        var response = await _agent.RunAsync<EditBlueprintStepResponse>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }
}
