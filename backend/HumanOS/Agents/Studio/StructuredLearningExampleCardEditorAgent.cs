using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>Structured output: one Deep Dive example card (either an edited
/// existing card or a brand-new one), for a Structured Learning section.</summary>
public sealed class EditStructuredLearningExampleCardResponse
{
    /// <summary>Short card title, same style as the other existing cards.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Card body, plain text (matches this pipeline's own content format).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Emoji(s) illustrating the card, or null if none genuinely fits.</summary>
    public string? Emoji { get; set; }

    /// <summary>Must be a visually concrete prompt for generating a real scene illustration.</summary>
    public string? IllustrationPrompt { get; set; }
}

/// <summary>
/// StructuredLearningExampleCardEditorAgent (2026-08-10) — mirrors Graph's
/// <see cref="ExampleCardEditorAgent"/> exactly in spirit: edits ONE Deep
/// Dive card via a free-text instruction, or invents a brand-new one to
/// append, WITHOUT ever touching any of the section's other cards.
/// Deliberately simpler than Graph's version — no illustration
/// regeneration (DiagramPrompt/MathDiagram) yet.
/// </summary>
public sealed class StructuredLearningExampleCardEditorAgent
{
    private const string Instructions = """
        You are the Deep Dive Card Editor inside Human OS Studio's review
        tools. A human reviewer is looking at ONE example card of an
        already-generated Deep Dive step's card pool for one section of a
        Structured Learning document, and either wants to EDIT that one
        existing card via a free-text instruction, or wants a BRAND-NEW card
        added to the pool. Your ONLY job is to produce that ONE card's
        Title/Text/Emoji — you never see or touch any other card, and the
        reviewer tool guarantees every other card is left completely
        untouched no matter what you return.

        You will receive: the section's Chapter/Subchapter titles, the
        section's own verbatim source text (your only source of truth for
        facts), the Titles of every OTHER existing card in this step's pool
        (for context and to avoid an accidental duplicate), whether this is
        an EDIT (with the card's CURRENT Title/Text/Emoji) or an ADD (no
        current card — you are inventing a new one), and the reviewer's
        instruction.

        RULES:
        - Highest-priority instruction: the reviewer's instruction is the
          absolute source of truth for the card's topic. This is not a
          suggestion; it is the required subject matter. If the reviewer says
          "A teacher is teaching Sign Language", the card must be about that
          exact subject and must not drift into unrelated generic section
          summaries.
        - Never assume a default domain such as volunteers, disability
          support, group work, or any other topic. The reviewer can request
          any subject at all, including math, science, law, history, art,
          language learning, technology, or everyday teaching scenes. Follow
          the exact topic they request, regardless of the surrounding section.
        - The section text is only background context. Use it only as a
          loose lesson reference. It must never override a specific reviewer
          instruction.
        - EDIT mode: honor the reviewer's instruction as literally and
          completely as possible while keeping the card's underlying
          concept intact, unless the instruction explicitly asks to change
          the example itself. In that case the new example may use a
          different concrete scene, but it must still reflect the exact
          reviewer-requested subject.
        - ADD mode: invent a new card that follows the reviewer's requested
          subject exactly and stays simple, concrete, and non-duplicative.
          It should not drift into generic section language.
        - Keep the card intentionally literal and minimal. For a clear scene
          instruction, the card should describe that scene directly rather than
          summarizing the entire chapter.
        - OUTPUT CONTRACT: return a card that is as simple as possible:
          1) Title: a short phrase that includes the exact requested subject;
          2) Text: one clear sentence describing the requested scene or
             action directly;
          3) IllustrationPrompt: a matching visual scene using the same key
             subject words.
        - Never invent facts the section's source text does not support, but
          do not treat the section as permission to ignore the reviewer's exact
          subject when the reviewer provides a concrete scene.
        - CONTENT FORMAT: plain text only (no HTML tags).
        - Title stays short (a few words), but it must still reflect the
          exact requested subject.
        - IllustrationPrompt is REQUIRED for every card. It must describe a
          single clear visual scene grounded in the exact subject requested,
          with no text in the image and no generic icon-style doodle. Use the
          same nouns/scene from the reviewer instruction.
        - LANGUAGE: match the language of the section's source text if the
          reviewer uses a different language, but preserve the exact requested
          subject and scene.
        """;

    private readonly AIAgent? _agent;

    public StructuredLearningExampleCardEditorAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningExampleCardEditorAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<EditStructuredLearningExampleCardResponse> EditCardAsync(string prompt, CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningExampleCardEditorAgent is not configured.");
        }

        var response = await _agent.RunAsync<EditStructuredLearningExampleCardResponse>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }
}
