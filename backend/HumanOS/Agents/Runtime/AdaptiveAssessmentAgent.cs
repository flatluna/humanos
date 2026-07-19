using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Runtime;

/// <summary>Structured output: one freshly-generated Assessment question.</summary>
public sealed class GeneratedAssessmentQuestionResponse
{
    /// <summary>The question text, in the same language as the node's content.</summary>
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>
    /// One of: ActiveRecall, Comprehension, Application, ErrorDetection,
    /// Transfer, Production, MultipleChoice (matches
    /// <see cref="Models.Learning.AssessmentQuestionType"/> exactly — the
    /// caller parses this string, defaulting to ActiveRecall if it doesn't
    /// match, rather than trusting an unparseable value).
    /// </summary>
    public string QuestionType { get; set; } = string.Empty;
}

/// <summary>Structured output: the grade for one answered Assessment question.</summary>
public sealed class GradedAssessmentAnswerResponse
{
    /// <summary>Raw quality score 0-100. Correctness label is derived from this in CODE, never trusted directly from the LLM.</summary>
    public int Score { get; set; }

    /// <summary>Specific, actionable feedback for the student about THIS answer.</summary>
    public string Feedback { get; set; } = string.Empty;

    /// <summary>Short label of the misconception/error revealed, or empty if none detected.</summary>
    public string ObservedError { get; set; } = string.Empty;
}

/// <summary>
/// AdaptiveAssessmentAgent — generates and grades the Memory-Paradox
/// compliant Assessment stage: exactly 5 DYNAMIC questions per round,
/// asked ONE AT A TIME, never a fixed question bank
/// ("no queremos preguntas repetidas ni exámenes estáticos"). If a round
/// fails (FinalScore &lt; 80), a brand-new round gets 5 completely NEW
/// questions that specifically target the errors observed in the failed
/// round — never a re-ask of the same questions.
///
/// Same simple "plain ChatClientAgent + structured output, LLM proposes /
/// code decides the objectively-checkable parts" pattern as
/// <see cref="AssessmentEvaluatorAgent"/> — deliberately NOT built on the
/// full Microsoft Agent Framework Workflow mandate (user-confirmed
/// 2026-07-18: keep this consistent with Runtime V1's existing
/// "grandfathered" Assessment lane rather than jumping ahead of
/// TutorAgent V2/CoachingAgent in the planned agent sequence — see
/// /memories/repo/agent-framework-native-architecture-mandate.md).
/// </summary>
public sealed class AdaptiveAssessmentAgent
{
    private const string QuestionGenerationInstructions = """
        You are the Adaptive Assessment question generator inside Human OS's
        Instructor Runtime. You receive full context about a Capability
        node (its academic definition, plain-language interpretation,
        examples, applications) and everything the student has already done
        in this learning session (Teaching/Recall/Production answers, and
        any errors observed so far in this Assessment round or a previous
        failed round). Your ONLY job is to generate ONE new, single
        assessment question right now — never the whole set at once, never
        a question you or a prior round already asked this student.

        MEMORY PARADOX PRINCIPLE — this governs everything: the AI must
        strengthen the student's own memory, thinking, learning and
        autonomy, never replace them. This is NOT a memorizable exam — it
        must look like a genuine demonstration of capability.

        QUESTION TYPE IS ASSIGNED TO YOU, NOT CHOSEN BY YOU: the prompt will
        tell you the exact REQUIRED QUESTION TYPE for this specific
        question (one of ActiveRecall, Comprehension, Application,
        ErrorDetection, Transfer, Production) — this was already picked
        deterministically to guarantee real variety across the round's 5
        questions. You MUST design a question whose underlying TASK
        genuinely exercises that cognitive skill, not just a reworded
        counting/recall task labeled differently. For example, an
        ErrorDetection question must show a flawed worked example and ask
        the student to find the mistake — it must NOT just be another
        "count these objects" prompt. A Comprehension question must ask the
        student to explain the concept in their own words or compare/
        contrast it — not perform another enumeration task.

        MultipleChoice may be used ONLY if the prompt explicitly says you
        are allowed to substitute it for this question, and even then only
        rarely, and only if it still demands real reasoning (not trivial
        recognition). If the prompt says not to use multiple-choice, do
        not.

        CONCISION — this is critical and frequently violated: ask ONE clear
        thing per question, not a checklist. Do NOT stack multiple sub-
        tasks with phrases like "después, también..., y finalmente
        explica...". A student should be able to read your question once
        and immediately know exactly what single thing to do.
        - ActiveRecall must be a SHORT, DIRECT retrieval of a specific fact
          or a computation with concrete values — e.g. for a math node,
          literally "¿Cuánto es 17 + 8?" or "¿Cuál es la fórmula de X?".
          It is NOT a request to define, explain, or justify the concept in
          your own words — that belongs to Comprehension, not ActiveRecall.
          If you catch yourself writing "explica" or "en tus propias
          palabras" for an ActiveRecall question, stop — you are actually
          writing a Comprehension question.
        - Comprehension: ONE request to explain/paraphrase/compare in their
          own words — not also asking for two extra examples on top.
        - Application/Transfer: ONE new concrete scenario, one computation
          or decision to make — not multiple layered sub-questions.
        - ErrorDetection: a short flawed example (2-3 lines max) plus ONE
          clear ask ("¿qué está mal y cuál es la respuesta correcta?") —
          not a 3-part checklist of things to also verify or demonstrate.
        - Production: ONE creative task — not a task plus a separate
          justification plus a separate verification step.

        RULES:
        - Must be clearly aligned with the node's own content — never test
          something outside what was actually taught.
        - Must genuinely measure understanding, not surface recognition.
        - Must be different from every question already listed as "already
          asked" in the prompt — never repeat wording or structure, AND
          never reuse the same underlying task shape (e.g. "count these N
          objects in this arrangement") across different question types.
        - If errors/confusions were observed (this round or a failed prior
          round), adapt this question to specifically probe or address one
          of them.
        - Adapt difficulty to the student's demonstrated level from the
          context given.
        - Write in the same language as the node's content.
        """;

    private const string GradingInstructions = """
        You are the Adaptive Assessment grader inside Human OS's Instructor
        Runtime. You receive one dynamically-generated assessment question,
        the full node context, and the student's actual answer to THAT
        question. Your ONLY job is to grade this ONE answer.

        SCORING (0-100), reflecting real demonstrated mastery of what this
        specific question asked:
        - 90-100: excellent, complete, correct.
        - 70-89: solid but with minor gaps or imprecision.
        - 40-69: partially correct — real understanding but a meaningful
          gap, error, or incomplete reasoning.
        - 0-39: incorrect, missing, or fails to address the question in a
          meaningful way.

        Ground your score STRICTLY in this answer against this question —
        never reward content the answer doesn't actually contain.

        FEEDBACK: specific, actionable, never a bare "correcto"/"incorrecto".
        Written directly to the student, supportive but honest tone, in the
        same language as their answer. Never reveal or hint at the answer to
        any FUTURE question.

        OBSERVED ERROR: if the answer reveals a specific misconception or
        gap, name it in a short label (e.g. "confunde perímetro con área").
        Leave this field as an empty string if nothing specific was
        detected — never invent one.
        """;

    private readonly AIAgent? _questionAgent;
    private readonly AIAgent? _gradingAgent;

    public AdaptiveAssessmentAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _questionAgent = null;
            _gradingAgent = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        var chatClient = client.GetChatClient(deploymentName);
        _questionAgent = chatClient.AsAIAgent(instructions: QuestionGenerationInstructions, name: "AdaptiveAssessmentQuestionAgent");
        _gradingAgent = chatClient.AsAIAgent(instructions: GradingInstructions, name: "AdaptiveAssessmentGradingAgent");
    }

    public bool IsConfigured => _questionAgent is not null && _gradingAgent is not null;

    /// <summary>Generates exactly one new assessment question from a fully-assembled context prompt.</summary>
    public async Task<GeneratedAssessmentQuestionResponse> GenerateQuestionAsync(string contextPrompt, CancellationToken cancellationToken = default)
    {
        if (_questionAgent is null)
        {
            throw new InvalidOperationException("AdaptiveAssessmentAgent is not configured (missing Azure OpenAI settings).");
        }

        var response = await _questionAgent.RunAsync<GeneratedAssessmentQuestionResponse>(contextPrompt, cancellationToken: cancellationToken);
        return response.Result;
    }

    /// <summary>Grades one student answer from a fully-assembled context prompt.</summary>
    public async Task<GradedAssessmentAnswerResponse> GradeAnswerAsync(string contextPrompt, CancellationToken cancellationToken = default)
    {
        if (_gradingAgent is null)
        {
            throw new InvalidOperationException("AdaptiveAssessmentAgent is not configured (missing Azure OpenAI settings).");
        }

        var response = await _gradingAgent.RunAsync<GradedAssessmentAnswerResponse>(contextPrompt, cancellationToken: cancellationToken);
        return response.Result;
    }
}
