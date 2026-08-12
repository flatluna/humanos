using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>Structured output: one freshly-generated Assessment question for a Structured Learning section.</summary>
public sealed class GeneratedStructuredLearningAssessmentQuestionResponse
{
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>Only set when asked for MultipleChoice — 3-5 options.</summary>
    public List<string>? Options { get; set; }

    /// <summary>Only set when asked for MultipleChoice (0-based).</summary>
    public int? CorrectOptionIndex { get; set; }
}

/// <summary>Structured output: the grade for one answered OpenEnded Assessment question.</summary>
public sealed class GradedStructuredLearningAssessmentAnswerResponse
{
    /// <summary>Raw quality score 0-100. Correctness label is derived from this in CODE, never trusted directly.</summary>
    public int Score { get; set; }

    public string Feedback { get; set; } = string.Empty;

    /// <summary>Short label of the misconception/error revealed, or empty if none.</summary>
    public string ObservedError { get; set; } = string.Empty;
}

/// <summary>
/// Real-time Assessment question generator/grader for Structured Learning
/// sections (2026-08-10) — replaces the OLD design where
/// <see cref="StructuredLearningSectionDesignerAgent"/> baked a FIXED list
/// of Assessment questions once at generation time (same static questions
/// shown to every student, every time). Mirrors the proven Graph/Memory
/// Paradox pattern (see Agents/Runtime/AdaptiveAssessmentAgent.cs): ONE
/// question generated fresh right before it's needed, never a fixed bank;
/// grading is a separate call. Same "LLM proposes, code decides"
/// discipline as that agent — <see cref="Services.StructuredLearningAssessmentEngine"/>
/// alone decides Correctness thresholds/FinalScore/Passed cutoff/round
/// chaining.
///
/// Deliberately simpler than AdaptiveAssessmentAgent: no per-question
/// cognitive-type rotation (Structured Learning's own AssessmentQuestionType
/// is a document-wide MultipleChoice/OpenEnded MODE, not a per-question
/// cognitive-skill label like Graph's ActiveRecall/Comprehension/...), and
/// no dynamic illustrations (kept out of scope for this pass).
/// </summary>
public sealed class StructuredLearningAssessmentAgent
{
    private const string Instructions = """
        You are the real-time Assessment question generator/grader for ONE
        section of a Structured Learning document (e.g. a policy/compliance
        chapter). You receive that section's own teaching content (never
        other sections) plus everything already asked/answered so far in
        the current round. Your ONLY job each call is either (a) generate
        ONE new assessment question, or (b) grade the student's answer to a
        question already asked — the prompt tells you which, never both.

        GENERATING A QUESTION:
        - Must be answerable from THIS section's own content alone — never
          invent facts, numbers, or rules the section doesn't state.
        - Must be genuinely DIFFERENT from every question already listed
          under "QUESTIONS ALREADY ASKED THIS ROUND" — never repeat one
          literally or with trivial rewording; ask about a different fact
          or angle each time.
        - Format: if told MultipleChoice, produce QuestionText plus 3-5
          Options with exactly one CorrectOptionIndex (0-based); if told
          OpenEnded, produce ONLY QuestionText (leave Options/
          CorrectOptionIndex null).
        - If errors/confusions were observed in earlier answers this round,
          prefer a question that revisits one of them.
        - Ask ONE clear thing — never stack multiple sub-questions.
        - LANGUAGE: match the language of the section content given to you.

        GRADING AN ANSWER (OpenEnded only — MultipleChoice is graded
        deterministically in code, never sent to you):
        - Score (0-100) how correct/complete the student's answer is
          against the EXACT question given to you — never a different
          question.
        - Feedback: short, specific, actionable, addressed to the student.
        - ObservedError: a short label of the misconception revealed, or
          empty string if none.
        """;

    private readonly AIAgent? _agent;

    public StructuredLearningAssessmentAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningAssessmentAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<GeneratedStructuredLearningAssessmentQuestionResponse> GenerateQuestionAsync(string prompt, CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningAssessmentAgent is not configured.");
        }

        var response = await _agent.RunAsync<GeneratedStructuredLearningAssessmentQuestionResponse>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }

    public async Task<GradedStructuredLearningAssessmentAnswerResponse> GradeAnswerAsync(string prompt, CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningAssessmentAgent is not configured.");
        }

        var response = await _agent.RunAsync<GradedStructuredLearningAssessmentAnswerResponse>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }
}
