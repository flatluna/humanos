using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Runtime;

/// <summary>Structured output: the grade for one Production submission.</summary>
public sealed class ProductionEvaluationResponse
{
    /// <summary>Raw quality score 0-100. Correctness is derived from this
    /// in CODE via <see cref="Agentic.Runtime.ProductionEvaluationGate"/>,
    /// never trusted directly from the LLM.</summary>
    public int Score { get; set; }

    /// <summary>Specific, actionable feedback for the student — always
    /// explains WHY the answer is right or wrong, never a bare
    /// "correcto"/"incorrecto".</summary>
    public string Feedback { get; set; } = string.Empty;
}

/// <summary>
/// ProductionEvaluatorAgent — formative (non-scoring) grading for the
/// "Aplícalo" (Production) step. Per <see cref="Studio.BlueprintValidatorAgent"/>'s
/// own definition of this step, a Production submission must CREATE,
/// APPLY, and TRANSFER the node's concept to a new/real situation — this
/// agent judges whether a specific submission actually does that.
///
/// Formative only: this grade is NEVER written to LearningAssessmentResult
/// and NEVER affects node mastery/unlocking — only the Assessment step (via
/// AdaptiveAssessmentEngine) counts toward that. Its only purpose is to
/// give the student real feedback and gate whether they can advance past
/// Production, so retrying is meaningful practice instead of a rubber-stamp.
///
/// Same simple "plain ChatClientAgent + structured output, LLM proposes /
/// code decides the objectively-checkable parts" pattern as
/// AssessmentEvaluatorAgent/AdaptiveAssessmentAgent.
/// </summary>
public sealed class ProductionEvaluatorAgent
{
    private const string Instructions = """
        You are the Production Evaluator agent inside Human OS's Instructor
        Runtime. "Production" ("Aplícalo") asks a student to invent or
        recognize a NEW real-world situation, apply the node's concept to
        it, compute/derive the result, and explain why the concept applies.
        You receive the Production prompt (from the Blueprint) and the
        student's actual submission. Your ONLY job is to judge THIS
        submission against that prompt's requirements.

        SCORING (0-100):
        - 90-100: excellent — a genuine new situation, correctly applied,
          correctly computed, clearly explained.
        - 70-89: acceptable — the core application is correct even if the
          explanation or computation has minor gaps.
        - 0-69: not yet — the submission is missing, merely repeats an
          example already given instead of inventing a new one, uses the
          wrong operation/concept, has a computation error, or fails to
          explain why it applies.

        Ground your score STRICTLY in what the submission actually
        contains — never assume intent it didn't state, and never reward a
        lucky correct number if the reasoning/application shown is wrong.

        FEEDBACK: ALWAYS explain WHY the submission is right or wrong —
        never a bare "correcto"/"incorrecto"/"muy bien". If wrong, name the
        specific mistake and what to fix, so the student can genuinely
        retry. If right, name specifically what they did well. Write
        directly to the student, supportive but honest tone, in the same
        language as their submission.
        """;

    private readonly AIAgent? _agent;

    public ProductionEvaluatorAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "ProductionEvaluatorAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>Evaluates one Production submission against its Blueprint prompt.</summary>
    public async Task<ProductionEvaluationResponse> EvaluateAsync(
        string productionPrompt,
        string studentSubmission,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("ProductionEvaluatorAgent is not configured (missing Azure OpenAI settings).");
        }

        var prompt = $"""
            PRODUCTION PROMPT (from the Blueprint):
            {productionPrompt}

            STUDENT SUBMISSION (what to actually evaluate):
            {studentSubmission}
            """;

        var response = await _agent.RunAsync<ProductionEvaluationResponse>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }
}
