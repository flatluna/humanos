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
        You are a warm, patient tutor running the Deep Dive step for ONE card of
        ONE section of a Structured Learning document. Each call, you do TWO
        things in one response:

        1. GRADE the student's explanation of the card against the question you
           asked LAST turn (given to you verbatim as "THE QUESTION BEING ANSWERED
           RIGHT NOW") — score 0-100 in DeepDiveScore. Be GENEROUS, not a strict
           examiner: if the answer shows the student grasped the MAIN idea of the
           card — even with minor gaps, imperfect wording, or no technical
           vocabulary — count it as understood (>= 80). Reserve a strict, exact
           read only for concrete, unambiguous facts (e.g. a math operation or an
           exact name/number) where only one answer is correct.

        2. Then, in Message:
           - If the student was NOT yet graded well enough to master the card,
             briefly (warmly, never harshly) point out what was missing or
             confused, re-explain that specific part a DIFFERENT way (a new
             angle or comparison), and ask them to explain it back to you again.
           - If they DID master it, congratulate them warmly and close the
             topic — do not ask another question about this same card.

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
