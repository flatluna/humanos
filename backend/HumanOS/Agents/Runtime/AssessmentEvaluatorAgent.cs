using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Runtime;

/// <summary>
/// The structured output of AssessmentEvaluatorAgent: a formal judgement of
/// whether a student's evidence satisfies the Assessment criteria a
/// NodeExperienceBlueprint already defines for a node.
/// </summary>
public sealed class AssessmentEvaluationResponse
{
    /// <summary>Overall quality/mastery score, 0-100.</summary>
    public int Score { get; set; }

    /// <summary>Actionable feedback for the student — never a bare "incorrecto"/"correcto".</summary>
    public string Feedback { get; set; } = string.Empty;
}

/// <summary>
/// Agente AssessmentEvaluator — Runtime Paso 3 of the Human OS Instructor
/// Runtime (see /memories/repo/human-os-runtime-design.md and the Runtime
/// V1 spec). Its ONLY job is to answer "¿Esta persona realmente dominó este
/// nodo?" by comparing the student's actual Assessment evidence against the
/// Assessment step's own criteria — it NEVER designs pedagogy, NEVER
/// modifies the Blueprint, NEVER decides graph progression/unlocking/
/// mastery/recommendations (all later Runtime Pasos).
///
/// Evidence != Assessment: Evidence is what the student did; this agent
/// produces the pedagogical INTERPRETATION of that evidence.
///
/// Plain ChatClientAgent with structured output — same pattern as
/// BlueprintValidatorAgent/ExperienceDesignerAgent (no Harness/Skills). The
/// deterministic Passed/Fail cutoff (Score >= 70) is computed in CODE by
/// <see cref="Services.AssessmentEvaluator"/> AFTER this call, not trusted
/// from the LLM — same "LLM proposes, code has final say for objectively-
/// checkable rules" pattern used by BlueprintValidationGuard.
/// </summary>
public sealed class AssessmentEvaluatorAgent
{
    private const string Instructions = """
        You are the Assessment Evaluator agent inside Human OS's Instructor
        Runtime. You receive the Assessment step's own success criteria
        (written by Studio's ExperienceDesigner agent, already validated)
        plus the real evidence a student produced during Hypothesis, Recall,
        Production and Assessment. Your ONLY job is to judge how well the
        student's Assessment evidence satisfies the stated criteria — you
        NEVER rewrite the criteria, NEVER invent new criteria, and NEVER
        decide anything about progression, unlocking, or mastery across
        nodes (this is about ONE node only).

        SCORING (0-100)
        - 90-100: Excellent mastery — evidence clearly and completely
          satisfies every criterion.
        - 70-89: Acceptable mastery — evidence satisfies the criteria well
          enough, possibly with minor gaps or imprecision.
        - 0-69: Not yet mastered — evidence is missing, incorrect, or fails
          to address one or more criteria in a meaningful way.

        Ground your score STRICTLY in the criteria given and the evidence
        given — never reward evidence for content it doesn't actually
        contain, and never penalize for things the criteria don't ask for.

        FEEDBACK
        Always produce SPECIFIC, ACTIONABLE feedback for the student — never
        a bare "correcto"/"incorrecto"/"incorrect". Name what they got right
        AND, if anything is missing or weak, name exactly what needs more
        precision or is absent. Write it directly to the student, in a
        supportive but honest tone, in the same language as their evidence.
        """;

    private readonly AIAgent? _agent;

    public AssessmentEvaluatorAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "AssessmentEvaluatorAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>Result of an evaluation call: the LLM's verdict plus the token usage of the call that produced it.</summary>
    public sealed class EvaluationResult
    {
        public AssessmentEvaluationResponse Evaluation { get; set; } = null!;

        public Studio.AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    /// <summary>
    /// Evaluates one node's Assessment evidence against its Assessment criteria.
    /// </summary>
    /// <param name="assessmentCriteria">The Assessment step's Content (the success criteria text, from the Blueprint).</param>
    /// <param name="assessmentEvidence">What the student actually answered during the Assessment step.</param>
    /// <param name="priorStepEvidence">Optional context: the student's Hypothesis/Recall/Production evidence, for a fuller picture.</param>
    public async Task<EvaluationResult> EvaluateAsync(
        string assessmentCriteria,
        string assessmentEvidence,
        IReadOnlyDictionary<string, string>? priorStepEvidence,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("AssessmentEvaluatorAgent is not configured (missing Azure OpenAI settings).");
        }

        var promptBuilder = new System.Text.StringBuilder();
        promptBuilder.AppendLine("ASSESSMENT CRITERIA (from the Blueprint):");
        promptBuilder.AppendLine(assessmentCriteria);
        promptBuilder.AppendLine();

        if (priorStepEvidence is not null)
        {
            foreach (var (stepName, evidence) in priorStepEvidence)
            {
                if (string.IsNullOrWhiteSpace(evidence))
                {
                    continue;
                }

                promptBuilder.AppendLine($"{stepName} EVIDENCE (context only):");
                promptBuilder.AppendLine(evidence);
                promptBuilder.AppendLine();
            }
        }

        promptBuilder.AppendLine("ASSESSMENT EVIDENCE (what to actually evaluate):");
        promptBuilder.AppendLine(assessmentEvidence);

        var response = await _agent.RunAsync<AssessmentEvaluationResponse>(promptBuilder.ToString());

        var usage = response.Usage;
        var tokenUsage = new Studio.AgentTokenUsage
        {
            AgentName = "AssessmentEvaluator",
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        return new EvaluationResult
        {
            Evaluation = response.Result,
            TokenUsage = tokenUsage
        };
    }
}
