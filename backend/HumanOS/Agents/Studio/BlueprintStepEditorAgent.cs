using Azure.AI.OpenAI;
using Azure.Identity;
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
}

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
