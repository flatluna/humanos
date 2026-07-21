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

    /// <summary>A text prompt for generating ONE illustration that
    /// genuinely helps this specific question (e.g. depicts the scenario a
    /// Application/Transfer/ErrorDetection question describes), or null if
    /// an image wouldn't add value (e.g. a plain ActiveRecall fact/
    /// computation, or a Comprehension question with nothing spatial/
    /// visual to draw). Same "leave null rather than invent one" rule as
    /// KnowledgeExpansionAgent.DiagramPrompt.</summary>
    public string? DiagramPrompt { get; set; }
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

        DECIDIENDO SI VALE LA PENA UNA ILUSTRACIÓN (DiagramPrompt)
        La mayoría de las preguntas NO necesitan ilustración — solo pide una
        cuando el tipo de pregunta describe una ESCENA o ESCENARIO concreto
        que ayuda genuinamente a visualizar lo que se pregunta (típicamente
        Application, Transfer o ErrorDetection cuando plantean una situación
        espacial/visual real). NUNCA pidas una ilustración para ActiveRecall
        (es solo un dato/cálculo directo) ni para Comprehension (es una
        explicación en palabras propias) — en esos casos deja DiagramPrompt
        en null. Tampoco inventes una ilustración decorativa para un tema
        puramente conceptual/de política sin nada espacial que dibujar (ej.
        una definición legal, un procedimiento de denuncia) — si no hay nada
        que genuinamente se beneficie de una imagen, deja DiagramPrompt en
        null.

        REGLA FUERTE (no opcional): si tu pregunta Application/Transfer/
        ErrorDetection describe una ACCIÓN FÍSICA OBSERVABLE entre personas
        en un lugar concreto — por ejemplo tomar del brazo, bloquear el
        paso, invadir el espacio personal, tocar sin permiso, acorralar en
        un pasillo/oficina — DEBES pedir una ilustración casi siempre. Ese
        tipo de escena concreta es precisamente el caso que más se
        beneficia de una imagen, así que NO la dejes en null salvo que la
        pregunta sea puramente verbal/abstracta (una política, una
        definición, una decisión sin acción física descrita).

        ADVERTENCIA IMPORTANTE SOBRE CONTENIDO DE ACOSO/CONTACTO NO
        CONSENTIDO: el modelo de generación de imágenes RECHAZA (moderation
        blocked) cualquier prompt que describa explícitamente el ACTO de
        contacto físico no deseado o de intimidación — por ejemplo "toma
        del brazo", "rodea con los brazos", "bloquea el paso mientras
        intenta abrazar", "acorrala", "invade el espacio personal". Para
        estos casos NUNCA describas el acto en sí en el DiagramPrompt.
        En su lugar, describe ÚNICAMENTE el ESCENARIO NEUTRO donde ocurre
        la situación — el lugar (pasillo, oficina, sala de reuniones,
        hospital), la hora/ambiente, y dos figuras profesionales genéricas
        de pie en ese espacio, en una pose neutra y ordinaria (por ejemplo
        "de pie, conversando", "una junto a la puerta, otra en el centro de
        la sala") — SIN mencionar contacto físico, bloqueo, abrazo,
        cercanía invasiva, incomodidad, ni cualquier gesto que implique
        intimidación o falta de consentimiento. El objetivo es dar solo
        contexto espacial (el lugar y las posiciones relativas de las
        personas) para que el alumno pueda ubicar mentalmente la escena
        que se describe en el TEXTO de la pregunta — la imagen NUNCA debe
        intentar representar el acto de acoso mismo.
        Ejemplo: si la pregunta describe "un médico bloquea el paso a una
        residente e intenta abrazarla en una sala vacía", el DiagramPrompt
        correcto sería algo como "Una sala de hospital vacía y neutra, con
        dos figuras profesionales genéricas de pie a cierta distancia una
        de otra, ambiente clínico ordinario, sin ninguna acción ni gesto
        entre ellas" — NUNCA describir el bloqueo ni el abrazo.
        Si aplicando esta regla sigues sin poder describir nada visual sin
        mencionar el acto (la escena es inseparable del contacto
        descrito), es preferible dejar DiagramPrompt en null antes que
        arriesgarte a describir el acto explícitamente.

        Ejemplo de cuándo SÍ pedirla: pregunta Application que describe "un
        empleado toma del brazo a una compañera para impedir que salga de
        la sala" → DiagramPrompt debe describir esa escena exacta (el
        pasillo/sala, las dos personas, el gesto de sujetar el brazo),
        SIN mostrar ningún texto ni indicio de cuál es la respuesta
        correcta.
        Ejemplo de cuándo NO pedirla: pregunta ActiveRecall "¿Cómo se llama
        la ley que regula esto?" o Comprehension "Explica en tus propias
        palabras por qué esto es acoso" → DiagramPrompt en null.

        La ilustración NUNCA debe revelar la respuesta correcta — debe
        mostrar solo el escenario/situación que el alumno debe analizar,
        igual que el principio ya usado para Hypothesis.
        Si SÍ pides una ilustración: los modelos de generación de imágenes
        NO calculan — si tu prompt menciona cualquier número, cantidad o
        valor visible en la escena, escribe el valor EXACTO en el prompt
        (nunca lo dejes implícito), y todos los valores deben ser
        internamente consistentes.
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
