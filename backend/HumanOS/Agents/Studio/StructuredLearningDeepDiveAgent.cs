using System.Collections.Concurrent;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>Structured output of one Deep Dive quiz turn.</summary>
public sealed class StructuredLearningDeepDiveTurnResponse
{
    /// <summary>What the agent says to the student this turn — either the
    /// card's own explanation plus a follow-up question (first attempt on
    /// a card), or a nudge + re-ask (retry). Always non-empty.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>0-100 score of how well the student's explanation
    /// demonstrated understanding of the CURRENT card. Null only when
    /// nothing has been graded yet (never happens here in practice — Deep
    /// Dive, unlike Recall, always has an implicit question from the
    /// card's own title, see <see cref="Services.StructuredLearningDeepDiveEngine"/>).</summary>
    public int? DeepDiveScore { get; set; }
}

/// <summary>
/// Real-time Deep Dive card quiz agent for Structured Learning sections
/// (2026-08-10) — mirrors <see cref="StructuredLearningRecallAgent"/>'s
/// shape exactly, adapted to Graph's own Deep Dive product rules (see
/// <see cref="HumanOS.Agentic.Runtime.DeepDiveLoopGate"/>'s doc comment):
/// the student must master EVERY card, strictly in order, no attempt cap,
/// no regression — just keep retrying the SAME card until cleared.
/// Deterministic mastery/card-advance decisions belong to the caller
/// (<see cref="Services.StructuredLearningDeepDiveEngine"/>, which reuses
/// <see cref="HumanOS.Agentic.Runtime.DeepDiveLoopGate"/> as-is) — same
/// "LLM proposes, code decides" discipline as every other agent here.
/// </summary>
public sealed class StructuredLearningDeepDiveAgent
{
    private const string Instructions = """
        You are a warm, patient tutor TEACHING one example card of a
        Structured Learning document — this is a REVIEW/EXAMPLE walkthrough,
        explicitly NOT a test or quiz. The student is never being examined
        here; your job is to explain the example clearly and make sure they
        feel free to ask questions, not to interrogate them. Each call, you
        do TWO things in one response:

        1. GRADE ONLY the student's own words as a genuine signal of
           understanding vs. confusion — score 0-100 in DeepDiveScore. Be
           VERY GENEROUS: any acknowledgment that they followed along (e.g.
           "entendido", "ok", "no tengo preguntas", "sigamos", "makes sense",
           a short paraphrase, or any answer that isn't clearly confused)
           counts as understood (>= 80) — never withhold advancement over
           imperfect wording, missing detail, or a short reply. Only score
           low when the student explicitly says they're confused or asks a
           question that shows a real misunderstanding.

        2. Then, in Message:
           - If this is the FIRST turn on this card (no prior question to
             grade), warmly EXPLAIN the example yourself in your own words —
             walk through it step by step like a teacher, grounded only in
             the card's own content — and END by asking if the student has
             any questions or wants you to continue to the next example.
           - If the student asked a genuine question or seemed confused,
             answer it warmly, re-explain that specific part a different way,
             and again ask if they have more questions or want to continue.
           - If the student indicated they're ready to move on (no
             questions, acknowledged, or gave a reasonable paraphrase),
             congratulate them briefly and close the topic — do not ask
             another quiz-style question about this same card.

        RULES:
        - Must be answerable from THIS card's own content alone — never invent
          facts beyond it.
        - When the card has an accompanying illustration (image attached
          and/or an IMAGE DESCRIPTION given in the prompt), your explanation
          MUST actually describe and reference what's shown in it (e.g. its
          labels, diagram parts, or depicted scene) — don't just restate the
          card's text as if there were no picture.
        - LANGUAGE: match the language of the card content given to you.
        - Always address the student directly and keep a warm, supportive,
          encouraging tone — never cold or robotic.
        """;

    private readonly AIAgent? _agent;

    /// <summary>One AgentSession per card conversation (2026-09-05), keyed
    /// by "{sectionStepId}:{cardIndex}" — keeps the whole back-and-forth on
    /// ONE card (initial explanation, follow-up questions, wrap-up) as a
    /// single flowing multi-turn conversation the model actually remembers,
    /// instead of a fresh stateless call every attempt. A new card always
    /// gets a brand-new key, so moving on naturally starts a clean session
    /// — no explicit reset needed. In-memory only (this agent is a
    /// singleton): lost on a process restart, same as every other
    /// in-memory-only piece of Deep Dive state before this change; the DB
    /// still persists CurrentDeepDiveCardIndex/CurrentDeepDivePrompt so the
    /// student's progress itself is never lost, only the model's own
    /// verbatim memory of the chat.</summary>
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();

    public StructuredLearningDeepDiveAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningDeepDiveAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>True the first time this card's session is asked for — the
    /// caller uses this to decide whether to attach the card's
    /// illustration(s) (only needed once; the session remembers them for
    /// every later turn on the same card).</summary>
    public bool IsFirstTurnOnCard(string sessionKey) => !_sessions.ContainsKey(sessionKey);

    /// <summary>Runs one turn of a flowing, multi-turn conversation about a
    /// single Deep Dive card — <paramref name="message"/> may include the
    /// card's own image(s) alongside its text (only needed on the first
    /// turn; later turns just carry the student's new reply since the
    /// session already remembers the card's content).</summary>
    public async Task<StructuredLearningDeepDiveTurnResponse> RunTurnAsync(string sessionKey, Microsoft.Extensions.AI.ChatMessage message, CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningDeepDiveAgent is not configured.");
        }

        if (!_sessions.TryGetValue(sessionKey, out var session))
        {
            session = await _agent.CreateSessionAsync(cancellationToken);
            _sessions[sessionKey] = session;
        }

        var response = await _agent.RunAsync<StructuredLearningDeepDiveTurnResponse>(message, session, cancellationToken: cancellationToken);
        return response.Result;
    }
}
