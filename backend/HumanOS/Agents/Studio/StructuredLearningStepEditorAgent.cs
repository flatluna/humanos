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
}

/// <summary>
/// StructuredLearningStepEditorAgent (2026-08-10) — Structured Learning's
/// own "Edit mode" review tool, mirrors <see cref="BlueprintStepEditorAgent"/>
/// (Graph's equivalent) exactly in spirit: lets a reviewer tweak ONE
/// already-generated step's Content via a free-text instruction before
/// publishing. Deliberately simpler than Graph's version — no illustration
/// regeneration (DiagramPrompt/MathDiagram) yet, plain text output (not
/// HTML, matching this pipeline's own Content format), only ever called for
/// Hypothesis/Teaching/Production (DeepDive is edited card-by-card via
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
        - Stay grounded STRICTLY in the section's own source text — never
          invent a fact, example, or sub-topic the source text doesn't
          support, even if the instruction asks for more detail.
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
