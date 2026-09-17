using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>Structured output: an edited Structured Learning section step.</summary>
public sealed class EditStructuredLearningStepResponse
{
    /// <summary>The step's new Content (plain text, same format as
    /// StructuredLearningSectionDesignerAgent's own output — no HTML tags).</summary>
    public string UpdatedContent { get; set; } = string.Empty;

    /// <summary>Set ONLY when the step's illustration should be
    /// regenerated (see the 3-case rule in Instructions below) — a fresh,
    /// concrete scene description for the image generator. Null/empty
    /// means: leave the step's existing illustration untouched.</summary>
    public string? IllustrationPrompt { get; set; }
}

/// <summary>
/// StructuredLearningStepEditorAgent (2026-08-10) — Structured Learning's
/// own "Edit mode" review tool, mirrors <see cref="BlueprintStepEditorAgent"/>
/// (Graph's equivalent) exactly in spirit: lets a reviewer tweak ONE
/// already-generated step's Content — and, when warranted, its illustration
/// (2026-08-21 — see the 3-case rule below) — via a free-text instruction
/// before publishing. Plain text output (not HTML, matching this
/// pipeline's own Content format). Only ever called for Hypothesis/
/// Teaching/Production (DeepDive is edited card-by-card via
/// <see cref="StructuredLearningExampleCardEditorAgent"/> instead; Recall/
/// Assessment have no static Content to edit — their questions are
/// generated live during practice).
/// </summary>
public sealed class StructuredLearningStepEditorAgent
{
    private const string Instructions = """
        You are the Section Step Editor inside Human OS Studio's review tools. A
        human reviewer is looking at ONE already-generated pedagogical step
        (Hypothesis, Teaching, or Production) of one section of a Structured
        Learning document, and wants to adjust it via a free-text instruction
        before the document is published. Your ONLY job is to apply that
        instruction to THIS one step, returning its updated Content.

        You will receive: the section's Chapter/Subchapter titles, the
        section's own verbatim source text (your only source of truth for
        facts), which StepType this is, the step's CURRENT Content, and the
        reviewer's instruction.

        RULES:
        - Honor the reviewer's instruction as literally and completely as
          possible while staying true to the step's own pedagogical purpose
          (never turn a Teaching step into a quiz, etc.).
        - Stay grounded STRICTLY in the section's own source text, supplemented
          by any verified web search grounding provided to you in the prompt —
          never invent an ungrounded fact or statistic not supported by either.
        - If verified web search findings (e.g. from Perplexity AI) are provided,
          seamlessly integrate the rich real-world examples, advanced applications,
          or deeper conceptual clarity into the lesson while preserving pedagogical focus.
        - If the instruction is vague ("make it better"), use your best
          pedagogical judgment to genuinely improve clarity and engagement
          for the section's audience — never just return the content
          unchanged.
        - CONTENT FORMAT: plain text only (no HTML tags), matching this
          pipeline's own existing content format.
        - LANGUAGE: match the language the step's CURRENT Content is written
          in, even if the reviewer's own instruction is written in a
          different language — never switch the document's content
          language just because the instruction itself was typed in
          another language.
        - TEACHING STEP & PROFESSOR SPOKEN DELIVERY:
          When editing a Teaching step, you MUST maintain its character as a
          universal spoken masterclass lecture delivered by the professor for all
          students (conversational, spoken cadence for audio TTS, no markdown
          headers, no bullet points). The lecture is generic for any student:
          do NOT include individual personal names (NO "Alejandro", NO "Student").
          Greet the class universally (e.g. "Hola y bienvenidos a esta clase...",
          "Hello and welcome to this lecture..."). Ground the teaching strictly in the
          authoritative content and general professional relevance for this domain.

        ILLUSTRATION REGENERATION — leave IllustrationPrompt null/empty by
        default (the step's existing illustration is untouched unless you
        set this). Only set it in ANY of these three cases:
          1. EXPLICIT REQUEST — the reviewer's instruction is clearly about
             the image itself (e.g. "change the image", "show 4 people each
             doing this sign", "add an illustration", "illustrate this").
          2. CONTRADICTION — your updated Content now describes a
             different concrete scene than before, so the OLD illustration
             would visually contradict the new text.
          3. AUTOMATIC/PROACTIVE — the instruction says nothing about images,
             but the updated Content describes a genuinely concrete,
             visualizable scenario that clearly deserves an illustration.
        When you do set IllustrationPrompt, describe ONE concrete scene
        grounded in the updated Content — for physical/gestural content
        (sign language, hand signs, dance/exercise moves, caregiving
        techniques, assistive-device usage, etc.) describe a real person's
        body/hands performing the exact motion(s) precisely (starting
        position, the motion itself, ending position), same as a
        photo/video demonstration frame — never a vague restatement, never a
        blackboard/poster of multiple items merged together, and never
        invent a detail the Content doesn't support. If the reviewer's
        instruction asks for MULTIPLE people/panels (e.g. "4 people each
        doing a different sign"), describe that exact composition explicitly
        (one clearly separated person per gesture, each labeled by position
        so the image generator can tell them apart) — otherwise, a single
        clear demonstration is enough.
        """;

    private readonly AIAgent? _agent;

    public StructuredLearningStepEditorAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningStepEditorAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<EditStructuredLearningStepResponse> EditStepAsync(string prompt, CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningStepEditorAgent is not configured.");
        }

        var response = await _agent.RunAsync<EditStructuredLearningStepResponse>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }
}
