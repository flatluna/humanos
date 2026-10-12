using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Generates ONE short, warm, in-character "Profe" spoken reaction to a
/// single tap in the Grouping card's recognition game (2026-10-08) — e.g.
/// "¡Sí! Ese es el sonido de la vaca, muy bien." or "Ese no es el sonido
/// del gato, escucha otra vez e inténtalo de nuevo." Deliberately a PLAIN
/// chat completion agent (no Realtime/WebRTC), same "cheap text sibling"
/// philosophy as <see cref="StructuredLearningQaProfessorAgent"/> — this is
/// step 1 (text+TTS) of a possible later step 2 (live Realtime voice
/// session), NOT itself a realtime conversation.
///
/// CORRECTNESS IS NEVER JUDGED BY THIS AGENT — the caller (frontend, via
/// EngramGroupingGame's deterministic tap-vs-target comparison) already
/// knows IsCorrect before calling this; this agent only phrases the
/// reaction naturally, with variety, never robotic repeated templates.
/// </summary>
public sealed class StructuredLearningEngramCardGameReactionAgent
{
    private const string Instructions = """
        You are "Profe", personally and warmly reacting, out loud, to ONE tap a young child (K-primary
        age) just made in a simple recognition game. You are given, in the user message: the CORRECT
        option's name, the correct option's RECOGNITION CUE (what was played — e.g. a sound, a math
        fact, a clue — so you can phrase your reaction naturally around it), what the child actually
        TAPPED, whether that tap was CORRECT, and the Language to respond in.

        Write EXACTLY ONE short sentence (occasionally two very short ones), as if speaking directly
        to the child right now, first person, warm and encouraging — never third-person, never
        clinical, never mention "game"/"AI"/"tap" in a technical way.

        If CORRECT: briefly, warmly confirm it, naturally referencing the correct answer (vary your
        phrasing every time — never the exact same template twice).

        If INCORRECT: gently say that what they picked is not the right one — you may naturally
        reference what they tapped — then warmly invite them to try again. NEVER reveal or hint at the
        actual correct answer when incorrect; just encourage another attempt.

        LANGUAGE (STRICT, NON-NEGOTIABLE): write your ENTIRE reaction in the exact Language given in
        the user message — 'es' means natural, simple, warm spoken Spanish a young child understands;
        'en' means natural, simple, warm spoken English. NEVER answer in a different language than the
        one given, regardless of what language the CORRECT/TAPPED option names happen to be written in.

        Output ONLY the spoken sentence itself — no labels, no "Profe:" prefix, no quotation marks,
        no markdown.
        """;

    private readonly AIAgent? _agent;
    private readonly string _deploymentName = string.Empty;

    public StructuredLearningEngramCardGameReactionAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            return;
        }

        _deploymentName = deploymentName;

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client.GetChatClient(deploymentName).AsAIAgent(instructions: Instructions, name: "StructuredLearningEngramCardGameReactionAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<string> ReactAsync(
        string correctTitle,
        string? correctCue,
        string tappedTitle,
        bool isCorrect,
        string language,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Structured Learning Engram Card Game Reaction Agent is not configured. Verify 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName'.");
        }

        var prompt =
            $"""
            CORRECT option: {correctTitle}
            CORRECT option's recognition cue (what was played): {correctCue ?? "(none)"}
            Child TAPPED: {tappedTitle}
            IsCorrect: {isCorrect}
            Language: {language}
            """;

        var response = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
        var text = response.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var isSpanish = string.Equals(language, "es", StringComparison.OrdinalIgnoreCase);
        return isCorrect
            ? (isSpanish ? "¡Muy bien!" : "Great job!")
            : (isSpanish ? "Intenta de nuevo." : "Try again.");
    }
}
