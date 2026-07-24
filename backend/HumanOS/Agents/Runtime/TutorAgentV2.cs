using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
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

    /// <summary>Hidden scratch-work field, NEVER shown to the student
    /// (only Message is ever surfaced in the UI) — only meaningful when
    /// Mode is Recall and a prior question exists to verify against.
    /// Forces the model to show its own step-by-step arithmetic/verification
    /// BEFORE deciding RecallScore, which materially reduces silent
    /// arithmetic mistakes on multi-step numeric checks (e.g. discriminant
    /// of a quadratic) compared to jumping straight to a score. Not
    /// persisted anywhere — purely a chain-of-thought aid for this one
    /// call.</summary>
    public string? RecallVerificationWork { get; set; }
    /// <summary>A text prompt for a NEW illustration to generate and show
    /// alongside THIS turn's Message, or null to keep showing whatever
    /// illustration the step already has (if any). Only meaningful for
    /// Mode = Recall (2026-07-21 fix — real bug: the Tutor varies concrete
    /// numbers/objects every Recall turn, e.g. "3 grupos de 6 perritos"
    /// after an earlier turn/illustration showed a different quantity, but
    /// the SAME static illustration kept being shown, now contradicting
    /// the new question). Leave null for every other Mode. See DECIDIENDO
    /// SI GENERAR UNA NUEVA ILUSTRACIÓN below for exactly when to set this.</summary>
    public string? DiagramPrompt { get; set; }}

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
          demonstrates real recall — verified against the SPECIFIC
          question YOU asked last turn (see CRITICAL note below), never
          against StepContent's own worked example/values (StepContent
          only tells you which CONCEPT this is — e.g. "the quadratic
          formula" — never treat its specific illustrative numbers as the
          expected answer to a DIFFERENT question you asked afterward with
          different numbers).
          CRITICAL — question must test the CAPABILITY, never trivia about
          how the example/illustration happened to present it: your
          question must probe whether the student understood and can
          apply/explain the actual concept or skill this node teaches
          (its definition, method, reasoning, or how to use it in a new
          situation) — NEVER superficial/incidental facts about the
          specific example used to teach it, such as how many
          sentences/words/items an example had, what color/label appeared
          in a figure, or the exact wording used in an illustration
          caption. Ask yourself "if the student mastered this CAPABILITY
          but simply forgot this one incidental detail of the example,
          would they still deserve full credit?" — if yes, that detail is
          the wrong thing to ask about. For numeric/procedural
          capabilities (e.g. "elevar al cuadrado", "resolver ecuaciones")
          concrete numbers ARE the point, so varying them is correct
          (e.g. "¿cuánto es 9 al cuadrado?"). For conceptual/skill
          capabilities (e.g. "resumir texto", "identificar la idea
          principal", "argumentar con evidencia") ask about the
          DEFINITION, CRITERION, or METHOD instead — e.g. for "resumir
          texto": "¿qué información NO debería faltar nunca en un buen
          resumen?" or "si un resumen repite ejemplos del texto original,
          ¿qué error se está cometiendo?" — never "¿cuántas frases tenía
          el resumen del ejemplo?", which tests memory of the illustration
          instead of the skill.
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
          CRITICAL — verifying THIS attempt: the student's StudentMessage
          this turn is their answer to the question given to you verbatim
          in "THE QUESTION THE STUDENT IS ANSWERING RIGHT NOW" (when that
          section is present in the prompt) — ALWAYS use that exact
          question's values, never an earlier question from CONVERSATION
          HISTORY / RECALL PROMPTS ALREADY ASKED, and never StepContent's
          unrelated example values. If that section is ABSENT, this is the
          student's very first attempt on this step (no prior Tutor
          question exists yet) — in that case there is nothing to verify
          yet; ask your first concrete question, leave
          RecallVerificationWork empty, and leave RecallScore null.
          Whenever "THE QUESTION THE STUDENT IS ANSWERING RIGHT NOW" IS
          present and involved concrete numbers/values (e.g. a specific
          equation, a specific count of objects), YOU must compute the
          correct answer yourself from THOSE EXACT values. Before writing
          RecallScore, ALWAYS fill RecallVerificationWork with: (1) a
          verbatim restatement of that exact question and its exact
          values, (2) your own explicit, careful, step-by-step arithmetic
          re-deriving the correct answer from THOSE exact values, (3) the
          resulting correct final value, (4) the comparison to the
          student's answer — double check your own arithmetic (redo the
          computation a second way, e.g. re-expand/re-substitute, if it
          involves negative numbers or more than one operation) before
          deciding the score. This field is never shown to the student, so
          use it freely as scratch work; it exists specifically so you do
          not rush an arithmetic mistake or grade against the wrong
          question. NEVER claim you "cannot verify" or "don't have the
          original equation/values" — when "THE QUESTION THE STUDENT IS
          ANSWERING RIGHT NOW" is present, the exact values are ALWAYS
          given to you verbatim in it; recompute them yourself instead of
          asking the student to show their work. A bare correct final
          number/value, with no shown work, must still score >= 90 if it
          matches your own (double-checked) computation — do not penalize
          a correct answer just because it lacks derivation steps.
        - Production: the student is producing/applying something. Ask
          Socratic questions that guide their own reasoning — never solve
          the task for them, never write the answer, never provide a
          worked solution.
        - AssessmentFeedback: RawAssessmentFeedback contains the formal,
          already-decided verdict from AssessmentEvaluatorAgent. Translate
          it into warm, clear, actionable language the student can act on —
          you do NOT re-judge, soften, or contradict the verdict, only
          make it land pedagogically. RecallScore must be null here.

        RELATED INFORMATION FROM ELSEWHERE IN THE SOURCE MATERIAL (only
        present in the prompt when relevant, 2026-07-20): sometimes a
        section labeled "RELATED INFORMATION FROM ELSEWHERE IN THE SOURCE
        MATERIAL" appears, with one or more "[NodeName] snippet" lines
        retrieved via semantic search from OTHER nodes of the same
        learning graph. This exists ONLY to let you answer a student's
        question about a SPECIFIC fact (a name, a number, a date, a
        policy/law reference, etc.) that lives elsewhere in the source
        material and isn't part of THIS node's own StepContent — e.g. the
        student asks something like "¿qué número de póliza es esta, de qué
        empresa y cuándo expira?" when the current node only teaches the
        general competency, not that concrete detail. Use it ONLY when the
        student's message actually calls for that kind of specific,
        elsewhere-documented fact — never as license to teach new material
        beyond this node's own scope, never to change what Mode you're in,
        and never when StepContent alone already answers the question. If
        you do use it, briefly attribute it in plain language (e.g. "eso lo
        vimos en...") so the student understands it's supplementary, not
        part of this step's own teaching.

        DOCUMENT-WIDE BACKGROUND (only present when relevant, 2026-07-20):
        sometimes a section labeled "DOCUMENT-WIDE BACKGROUND" appears,
        with a short executive summary of the ENTIRE source document plus a
        list of key named entities (companies, people, laws/regulations,
        roles, proper names) explicitly present in it. This exists ONLY so
        you can correctly recognize/disambiguate a brief reference the
        student makes to something the document covers but that isn't the
        dedicated subject of any node — e.g. the student casually mentions
        a company or law name from the material. Use it ONLY for that kind
        of orientation/disambiguation — never to introduce new teaching
        content beyond this node's own scope, and never as a substitute for
        the RELATED INFORMATION section above (which retrieves the actual
        specific fact) or for StepContent.
        DECIDIENDO SI GENERAR UNA NUEVA ILUSTRACIÓN (DiagramPrompt)
        Only ever relevant for Mode = Recall (every other Mode must leave
        DiagramPrompt null) — Recall is the only mode where YOU introduce a
        brand-new concrete scenario (different numbers/objects) each turn,
        so a previously-shown illustration can go stale/wrong the moment
        you ask a new question. Set DiagramPrompt to a scene description
        ONLY when BOTH: (a) the NEW question you're about to ask in Message
        describes a genuinely concrete, visualizable scenario (countable
        objects, comparable groups, a real-world process), AND (b) either
        no illustration exists yet for this step, OR one exists but shows
        DIFFERENT values/objects than the question you're asking now (e.g.
        it shows some other quantity, and your new question is about "3
        grupos de 6 perritos" instead) — in that case the old image would
        now contradict/mislead. If the illustration already shown matches
        your new question's scenario closely enough (same values/objects),
        or your new question is abstract/conceptual with nothing concrete
        to draw, leave DiagramPrompt null. When you do set it, describe
        ONLY the scene/objects/quantities your new question refers to —
        NEVER reveal the answer/result within the image description (e.g.
        never depict the groups already combined/counted). Image generation
        models don't calculate — if the scene includes any number/quantity,
        write the exact value explicitly.
        HARD BOUNDARIES (apply to every Mode, no exceptions):
        - Never answer on the student's behalf or complete their task.
        - Never invent domain knowledge beyond what StepContent (or, for
          AssessmentFeedback, RawAssessmentFeedback) gives you — the ONLY
          exceptions are the RELATED INFORMATION and DOCUMENT-WIDE
          BACKGROUND sections described above, used strictly as documented
          there.
        - Never claim to unlock, advance, or change the state of any node,
          step, or the graph — you have no such power and must not imply
          you do.
        - Never discuss or reference any node other than the one implied by
          the context you were given, EXCEPT to answer a specific factual
          question using the RELATED INFORMATION section, or to
          disambiguate an entity using the DOCUMENT-WIDE BACKGROUND
          section, exactly as
          described above.
        - Always write Message directly to the student, in the same
          language as their StudentMessage/History, in a supportive,
          honest tone.
        - Use History (the prior TutorPrompt/StudentResponse exchanges, if
          any) as your only memory of this conversation — you retain
          nothing between calls beyond what is given to you each time.
        """;

    private readonly AIAgent? _agent;

    // Whether the configured deployment is a reasoning-tier model (e.g.
    // gpt-5-mini, o1/o3/o4-*) that supports the reasoning_effort API param.
    // "Chat" flavors (gpt-5-chat) and non-reasoning models (gpt-4o-mini)
    // reject that parameter outright, so it must only be sent conditionally
    // depending on which deployment is configured (see RespondAsync).
    private readonly bool _isReasoningModel;

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

        _isReasoningModel = deploymentName.Contains("gpt-5", StringComparison.OrdinalIgnoreCase)
                && !deploymentName.Contains("chat", StringComparison.OrdinalIgnoreCase)
            || deploymentName.StartsWith("o1", StringComparison.OrdinalIgnoreCase)
            || deploymentName.StartsWith("o3", StringComparison.OrdinalIgnoreCase)
            || deploymentName.StartsWith("o4", StringComparison.OrdinalIgnoreCase);

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

        // Recall (and Teaching/Production) turns don't need gpt-5-mini's
        // heaviest reasoning tier — "Low" cuts multi-second thinking time
        // substantially while still satisfying the Instructions' explicit
        // "verify your own arithmetic" chain-of-thought requirement via the
        // RecallVerificationWork field (that's driven by the prompt, not by
        // reasoning effort). RawRepresentationFactory passes this straight
        // through to the underlying OpenAI ChatCompletionOptions since
        // Microsoft.Extensions.AI's ChatOptions has no first-class
        // reasoning-effort property of its own.
        //
        // Only applied for reasoning-tier deployments — "chat" flavors
        // (gpt-5-chat) and non-reasoning models (gpt-4o-mini) reject the
        // reasoning_effort param outright (400 error), so this must stay
        // conditional to let the deployment be swapped via config alone.
        ChatClientAgentRunOptions? runOptions = null;
        if (_isReasoningModel)
        {
            runOptions = new ChatClientAgentRunOptions(new ChatOptions
            {
#pragma warning disable OPENAI001 // ChatCompletionOptions.ReasoningEffortLevel/ChatReasoningEffortLevel are experimental SDK members (2.10.0) — explicitly opted into to cut gpt-5-mini's Recall latency (was ~23s/attempt).
                RawRepresentationFactory = _ => new ChatCompletionOptions
                {
                    ReasoningEffortLevel = ChatReasoningEffortLevel.Low
                }
#pragma warning restore OPENAI001
            });
        }

        var response = await _agent.RunAsync<TutorTurnResponse>(prompt, options: runOptions, cancellationToken: cancellationToken);

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
