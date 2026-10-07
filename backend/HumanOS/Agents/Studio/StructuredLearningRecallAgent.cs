using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>Structured output of one Recall practice turn.</summary>
public sealed class StructuredLearningRecallTurnResponse
{
    /// <summary>What the agent says to the student this turn — a brief
    /// nudge if their last answer was close-but-not-quite (never the
    /// answer itself), ending with a NEW retrieval question. Always
    /// non-empty.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Ephemeral 0-100 score of how well the student's answer
    /// demonstrated real recall of the PREVIOUS question, WITHOUT help.
    /// Null only on the very first turn (no prior question to grade yet).</summary>
    public int? RecallScore { get; set; }

    /// <summary>Hidden scratch-work field, never shown to the student —
    /// only meaningful when a prior question exists to verify against.
    /// Forces step-by-step verification before deciding RecallScore (same
    /// technique as TutorAgentV2 — measurably reduces silent grading
    /// mistakes on concrete numeric/factual answers).</summary>
    public string? RecallVerificationWork { get; set; }
}

/// <summary>
/// Real-time Recall question generator/grader for Structured Learning
/// sections (2026-08-10) — replaces the OLD design where
/// <see cref="StructuredLearningSectionDesignerAgent"/> baked a FIXED list
/// of Recall items once at generation time (same static items shown to
/// every student, every time). Mirrors the proven Graph/Memory Paradox
/// pattern (see Agents/Runtime/TutorAgentV2.cs's Recall mode): ONE call
/// does BOTH things each turn — grades the answer to the question asked
/// last turn (if any) AND generates the NEXT question — never a fixed
/// bank. Deterministic mastery/attempt-budget decisions belong to the
/// caller (<see cref="Services.StructuredLearningRecallEngine"/>, which
/// reuses <see cref="HumanOS.Agentic.Runtime.RecallLoopGate"/> as-is —
/// same "LLM proposes, code decides" discipline as TutorAgentV2/
/// RecallLoopGate).
///
/// Deliberately simpler than TutorAgentV2: only the Recall mode exists
/// here (no Teaching/Production/DeepDive/AssessmentFeedback branches,
/// since Structured Learning has no equivalent runtime yet for those),
/// and no dynamic illustrations (kept out of scope for this pass).
/// </summary>
public sealed class StructuredLearningRecallAgent
{
    private const string Instructions = """
        You are the real-time Recall practice partner for ONE section of a
        Structured Learning document. The student is trying to recall a
        concept from this section without external help. Each call, you do
        TWO things in one response:

        1. GRADE the student's answer to the question you asked LAST turn
           (given to you verbatim as "THE QUESTION BEING ANSWERED RIGHT
           NOW") — score 0-100 in RecallScore, verified against THAT EXACT
           question, never against the section's own unrelated example
           values. If that section is ABSENT from the prompt, this is the
           student's very first turn (no prior question exists yet) —
           leave RecallScore null and RecallVerificationWork empty; there
           is nothing to grade yet.

           CRITICAL — question must test the CAPABILITY, never trivia
           about how the section happened to present it: ask yourself "if
           the student mastered this idea but simply forgot an incidental
           detail of the example, would they still deserve full credit?" —
           if yes, that detail is the wrong thing to grade harshly.

           Before writing RecallScore (when a question exists to verify
           against), fill RecallVerificationWork with: a verbatim
           restatement of that exact question, your own careful
           step-by-step re-derivation of the correct answer from it, and
           the comparison to the student's answer — this scratch space is
           never shown to the student. A bare correct final answer, with no
           shown work, must still score >= 90 if it matches your own
           (double-checked) derivation.

        2. Then, in Message, ask a NEW, SHORT, DIRECT retrieval question —
           different concrete values/wording than every prompt listed under
           "QUESTIONS ALREADY ASKED" (if any) — never literally repeat one,
           never just reword the same scenario with synonyms. Fold in a
           brief nudge only if the student's last answer was close but not
           quite there — never the answer itself, and never state the
           numeric score to the student directly.

        RULES:
        - Must be answerable from THIS section's own content alone — never
          invent facts beyond it.
        - Never teach anything new inside the question itself.
        - LANGUAGE: match the language of the section content given to you.
        - Always address the student directly and keep a warm, supportive tone.
        """;

    private readonly AIAgent? _agent;
    private readonly string _deploymentName = string.Empty;

    public StructuredLearningRecallAgent(IConfiguration configuration)
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

        _agent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningRecallAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<StructuredLearningRecallAgentTurnResult> RunTurnAsync(string prompt, CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningRecallAgent is not configured.");
        }

        var response = await _agent.RunAsync<StructuredLearningRecallTurnResponse>(prompt, cancellationToken: cancellationToken);
        return new StructuredLearningRecallAgentTurnResult
        {
            Turn = response.Result,
            ModelName = _deploymentName,
            InputTokens = (int)(response.Usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(response.Usage?.OutputTokenCount ?? 0),
        };
    }
}

/// <summary>One Recall turn's result + real token usage from that call —
/// see <see cref="StructuredLearningRecallAgent.RunTurnAsync"/>.</summary>
public sealed class StructuredLearningRecallAgentTurnResult
{
    public StructuredLearningRecallTurnResponse Turn { get; set; } = null!;

    public string ModelName { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }
}
