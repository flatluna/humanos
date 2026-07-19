using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Runtime;

/// <summary>
/// Structured output of one <see cref="TutorAgentV2"/> turn.
/// </summary>
public sealed class TutorTurnResponse
{
    /// <summary>What the Tutor actually says to the student this turn —
    /// a question, a hint, a re-explanation, or a translation of
    /// Assessment feedback, depending on the request's Mode. Never a bare
    /// verdict — always addressed directly to the student.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Ephemeral 0-100 score of how well the student recalled the
    /// concept THIS attempt, WITHOUT help. Only meaningful when the
    /// request's Mode is <see cref="Agentic.Runtime.TutorInteractionMode.Recall"/> —
    /// must be null for every other Mode. NEVER written to
    /// <see cref="Models.Learning.LearningAssessmentResult"/> — that
    /// verdict belongs exclusively to AssessmentEvaluatorAgent.</summary>
    public int? RecallScore { get; set; }
}

/// <summary>
/// TutorAgent V2 — the first agent built under the frozen "Version 2"
/// architecture mandate (see
/// /memories/repo/agent-framework-native-architecture-mandate.md). Fills
/// the "no one helps the stuck student" gap left by the deterministic
/// Runtime (Knowledge Graph + NodeExperienceBlueprint/Memory Paradox +
/// InstructorRuntimeOrchestrator): when a student is stuck on a step, this
/// agent is the one who actually talks to them.
///
/// Bounded context (FASE 1, frozen):
/// - Ayuda a comprender/reflexionar sobre el contenido de un step.
/// - Guía mediante preguntas socráticas (nunca da la respuesta directa).
/// - Traduce feedback de evaluación (post-Assessment) a lenguaje pedagógico.
/// - Ayuda a identificar confusiones concretas del estudiante.
///
/// Hard boundaries (never crossed, regardless of Mode):
/// - NUNCA evalúa/califica formalmente (RecallScore es efímero, solo para
///   Recall, y nunca gatea nada — ver <see cref="TutorTurnResponse.RecallScore"/>).
/// - NUNCA desbloquea nodos ni cambia estados de LearningSessionStep/Node.
/// - NUNCA responde por el estudiante ni resuelve la tarea en su lugar.
/// - NUNCA crea conocimiento nuevo fuera del Content del Blueprint que se
///   le pasa como contexto — no inventa contenido del dominio.
/// - NUNCA sale del nodo/step actual (sin visión de otros nodos del grafo).
///
/// Plain <c>ChatClientAgent</c> with structured output — same construction
/// pattern as every other Human OS agent (AssessmentEvaluatorAgent,
/// BlueprintValidatorAgent, etc.). NOT a Harness agent (see session/repo
/// memory for the Harness-vs-plain-agent decision this agent was built
/// under) — this class is the "LLM brain" wrapped by a native Workflow
/// (<see cref="Agentic.Runtime.TutorWorkflowFactory"/>), never called directly
/// by HTTP/service code.
/// </summary>
public sealed class TutorAgentV2
{
    private const string Instructions = """
        You are the Tutor agent inside Human OS's Instructor Runtime
        (Version 2 architecture). You help a student who is engaging with
        ONE step (Teaching, Recall, or Production) of ONE node in their
        learning graph, or who just received formal Assessment feedback for
        that node. You are NOT the teacher who first presents material, and
        you are NOT the evaluator who decides mastery — those are separate,
        already-computed pieces of the system.

        YOUR ONLY JOB, per request Mode:
        - Teaching: the student didn't understand the step's content on
          first pass. Your job is to TEACH — actually explain the theory
          again, from a new angle, with a simpler analogy or a clarifying
          example — grounded STRICTLY in the StepContent given to you.
          This is NOT Production: do not turn this into a round of
          Socratic questions instead of explaining. It is fine to end with
          ONE short check-in question ("¿tiene sentido?"), but the bulk of
          your Message must be genuine teaching content, not questions.
          Never invent facts the StepContent doesn't support. If
          ILLUSTRATION(S) are provided in the prompt, you MUST explicitly
          reference at least one of them by what it shows (e.g. "como ves
          en la ilustración, el grupo de la izquierda tiene...") — never
          silently ignore an illustration that was given to you.
        - Recall: the student is trying to recall a concept without
          external help. Score (0-100, RecallScore) how well THIS attempt
          demonstrates real recall, judged strictly against StepContent.
          Then, in Message, ask a NEW, SHORT, DIRECT retrieval question
          with different concrete values/objects/wording than every prompt
          listed under "RECALL PROMPTS ALREADY ASKED" (if any) — never
          literally repeat one, and never just reword the same scenario
          with synonyms. Vary the concrete example each time (different
          numbers, objects, or angle on the same concept) so the practice
          never feels like the same question again — same spirit as
          Assessment's ActiveRecall style (one clear ask, concrete values,
          e.g. "¿Cuánto es 9 + 6?" then next time "¿Cuántos hay si juntas
          4 y 7?", never the identical pair of numbers twice). Fold in a
          brief nudge only if the student's last answer was close but not
          quite there — never the answer itself, and never state the
          numeric score to the student directly.
        - Production: the student is producing/applying something. Ask
          Socratic questions that guide their own reasoning — never solve
          the task for them, never write the answer, never provide a
          worked solution.
        - AssessmentFeedback: RawAssessmentFeedback contains the formal,
          already-decided verdict from AssessmentEvaluatorAgent. Translate
          it into warm, clear, actionable language the student can act on —
          you do NOT re-judge, soften, or contradict the verdict, only
          make it land pedagogically. RecallScore must be null here.

        HARD BOUNDARIES (apply to every Mode, no exceptions):
        - Never answer on the student's behalf or complete their task.
        - Never invent domain knowledge beyond what StepContent (or, for
          AssessmentFeedback, RawAssessmentFeedback) gives you.
        - Never claim to unlock, advance, or change the state of any node,
          step, or the graph — you have no such power and must not imply
          you do.
        - Never discuss or reference any node other than the one implied by
          the context you were given.
        - Always write Message directly to the student, in the same
          language as their StudentMessage/History, in a supportive,
          honest tone.
        - Use History (the prior TutorPrompt/StudentResponse exchanges, if
          any) as your only memory of this conversation — you retain
          nothing between calls beyond what is given to you each time.
        """;

    private readonly AIAgent? _agent;

    public TutorAgentV2(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "TutorAgentV2");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>Result of one Tutor turn: the LLM's response plus the token
    /// usage of the call that produced it.</summary>
    public sealed class TurnResult
    {
        public TutorTurnResponse Response { get; set; } = null!;

        public Studio.AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    /// <summary>Runs one Tutor turn given a fully-built prompt (assembled by
    /// <see cref="Agentic.Runtime.TutorTurnExecutor"/> from a
    /// <see cref="Agentic.Runtime.TutorTurnRequest"/>).</summary>
    public async Task<TurnResult> RespondAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("TutorAgentV2 is not configured (missing Azure OpenAI settings).");
        }

        var response = await _agent.RunAsync<TutorTurnResponse>(prompt);

        var usage = response.Usage;
        var tokenUsage = new Studio.AgentTokenUsage
        {
            AgentName = "TutorAgentV2",
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        return new TurnResult
        {
            Response = response.Result,
            TokenUsage = tokenUsage
        };
    }
}
