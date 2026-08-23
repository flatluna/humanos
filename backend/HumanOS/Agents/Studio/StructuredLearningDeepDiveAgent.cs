using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
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
        - LANGUAGE: match the language of the card content given to you.
        - Always address the student directly and keep a warm, supportive,
          encouraging tone — never cold or robotic.
        """;

    private readonly AIAgent? _agent;

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

    public async Task<StructuredLearningDeepDiveTurnResponse> RunTurnAsync(string prompt, CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningDeepDiveAgent is not configured.");
        }

        var response = await _agent.RunAsync<StructuredLearningDeepDiveTurnResponse>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }
}
