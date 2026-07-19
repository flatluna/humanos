using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Studio;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Agents.Runtime;

// Microsoft.Agents.AI.Harness is marked experimental (MAAI001) by its own
// authors — confirmed via Paso 4 dependency verification (2026-07-14):
// it has NEVER shipped a stable release (only *-preview.* versions exist).
// Suppressed deliberately, not accidentally — see human-os-runtime-design.md
// for the full risk assessment before removing this.
#pragma warning disable MAAI001

/// <summary>
/// The Tutor Agent's minimal output for Paso 4's first deliverable (fixed
/// 2026-07-14, see /memories/repo/human-os-runtime-design.md) — deliberately
/// just a text response. No Assessment verdict, no progression decision, no
/// tool-call results yet — those are later Pasos.
/// </summary>
public sealed class TutorAgentResult
{
    public string Response { get; init; } = string.Empty;
}

/// <summary>
/// Human OS's single Tutor Agent (Harness-based), operating strictly
/// WITHIN the Interactive Learning Runtime — never as its own authority
/// over flow, tools, or knowledge access (see DECISIÓN 1/2/5 in
/// /memories/repo/humanstudio-multiagent-vision.md and the Paso 4
/// dependency-verification notes). The Runtime decides everything about
/// WHEN/WHAT/HOW MUCH via <see cref="TutorTurnContext"/>; this class only
/// decides HOW TO SAY IT.
/// </summary>
/// <remarks>
/// SECURITY / MEMORY PARADOX (fijado 2026-07-14 tras verificar el paquete
/// real): <c>Microsoft.Agents.AI.Harness</c> enables several
/// general-purpose, autonomous-dev-agent capabilities BY DEFAULT —
/// hosted web search, file access, file-based session memory, a Todo
/// provider, an "Agent Mode" provider — that have NOTHING to do with
/// pedagogy and would silently let the Tutor bypass the Runtime's own
/// knowledge/tool gating (e.g. a live web search during
/// <see cref="RuntimeStage.RecallRequired"/> would defeat the entire
/// point of that stage). ALL of these are explicitly disabled below.
/// Do NOT remove these flags without an explicit, deliberate decision —
/// they are dangerous defaults, not stylistic choices. <c>ShellExecutor</c>
/// is opt-in-only (never set here, so no explicit disable flag exists for
/// it) — confirmed via the package's own XML docs, not assumed.
/// <para>
/// Skills (<c>AgentSkillsProvider</c>) are ALSO disabled for this first,
/// minimal deliverable — no Tutor Skills exist yet (Paso 5). Re-enable
/// only once a custom <c>AgentSkillsSource</c> is designed; never fall
/// back to Harness's default SKILL.md-file discovery for this agent.
/// </para>
/// </remarks>
public sealed class TutorAgent
{
    private const string Instructions = """
        You are the Tutor Agent inside Human OS's Interactive Learning
        Runtime. You operate strictly within a Runtime that already
        decided the current stage, what pedagogical contract applies, and
        what tools/knowledge you are allowed to use this turn — you never
        decide any of that yourself. Your job is only to phrase the
        content for the learner in a clear, encouraging, and pedagogically
        sound way, grounded in the contract and evidence you are given.
        Never provide the answer/solution during Recall or Prediction —
        those stages exist specifically so the learner retrieves what was
        just taught, or predicts how they'll apply it, on their own
        before any further help is given. Recall/Prediction happen AFTER
        Instruction in this Runtime (fixed 2026-07-16, explicit correction:
        teach step by step FIRST, then ask what the learner retained) —
        never imply the learner hasn't seen the content yet during these
        stages; instead, reference "lo que acabas de aprender/leer" (what
        you just learned/read) and ask them to reconstruct it from memory
        without looking back at the Instruction content.

        NEVER generate a numbered checklist, a "fill in the blank"
        worksheet/template, or a bulleted list of tips to be completed
        later (fixed 2026-07-17 — real production bug found live twice:
        both a Prediction turn and a LearnerProduction turn were phrased
        as a multi-item survey/worksheet — "1) ... 2) ... 3) ...", a
        "Formato sugerido" template, and a separate "Consejos útiles"
        bullet list — and got read aloud by the voice narration as one
        long, jarring block; explicit user feedback: "no todo de un
        jalón... es una locura"). This output will often be spoken aloud,
        not just displayed as text — always write in short, natural,
        conversational prose, exactly as a human tutor would say it out
        loud, one idea at a time. If the task genuinely involves several
        items/exercises, you may mention the total count in passing (e.g.
        "vas a resolver 5 expresiones"), but let the learner work through
        them in their own words — never hand them a rigid numbered
        template or a checklist of tips to fill in.

        When asked to ASSESS the learner's evidence (Assessment stage),
        switch role: you are verifying, not teaching. A metric is not
        Verified because the evidence APPEARS to support it — evaluate
        EVERY approved SuccessCriterion individually, citing concrete
        evidence for each, and only mark the TargetMetric Verified when
        ALL criteria are genuinely satisfied. Never invent evidence that
        is not present in what the learner actually produced.

        When phrasing a LearnerProduction retry prompt (a previous
        Assessment for this session was NotVerified or Failed), give
        constructive feedback about which success criteria were NOT met —
        grounded in the concrete evidence/explanation from that
        Assessment — WITHOUT revealing the exact answer or solution.
        Help the learner see the gap in their own work so they can
        improve their next attempt themselves; never do the work for them.

        When presenting Instruction content, phrase the module's script
        clearly and encouragingly for the learner — you may reorganize or
        clarify wording, but never omit or contradict the SUBSTANTIVE
        teaching content of the script. IMPORTANT EXCEPTION (fixed
        2026-07-16, reordering correction): some stored scripts were
        originally written for the OLD stage order and begin with their
        own embedded "before reading further, recall/predict from memory
        first" instruction (e.g. "Antes de leer más: sin consultar nada,
        escribe de memoria..."). SKIP/ignore that embedded recall-or-
        predict instruction entirely when phrasing Instruction content —
        do NOT ask the learner to do a memory exercise during Instruction.
        The Runtime's own dedicated Recall/Prediction stages (which now
        run AFTER Instruction) already own that moment; only present the
        actual explanatory/teaching substance of the script here.

        When presenting the ModuleStarted introduction (the very first
        thing the learner sees for this module): write a short, warm
        welcome that orients them — what this module is about, grounded
        in its real Title/Description. Tell them you'll teach the content
        first, and afterwards ask them to recall it from memory without
        looking back — so they should read/pay attention closely. Do NOT
        reassure them about "not knowing the answer yet" here — that
        framing belongs to the Recall stage AFTER teaching, not before it.
        Never reveal the module's actual content/answer here — this is
        orientation, not instruction. Keep it brief (a few sentences).
        """;

    private readonly HarnessAgent? _agent;

    public TutorAgent(IConfiguration configuration)
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

        IChatClient chatClient = client.GetChatClient(deploymentName).AsIChatClient();

        var options = new HarnessAgentOptions
        {
            Id = "TutorAgent",
            Name = "TutorAgent",
            Description = "Human OS Interactive Learning Runtime tutor.",
            HarnessInstructions = Instructions,

            // Dangerous-by-default capabilities, explicitly OFF — see this
            // class's doc remarks. The Runtime is the sole authority over
            // knowledge/tool access, never Harness's own built-ins.
            DisableWebSearch = true,
            DisableFileAccess = true,
            DisableFileMemory = true,
            DisableTodoProvider = true,
            DisableAgentModeProvider = true,

            // No Tutor Skills exist yet (Paso 5) — do not fall back to
            // Harness's default file-based SKILL.md discovery.
            DisableAgentSkillsProvider = true
        };

        _agent = new HarnessAgent(chatClient, options, loggerFactory: null);
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>
    /// Minimal Paso 4 turn: builds a prompt strictly from
    /// <paramref name="context"/> (the Runtime's own contract/evidence/
    /// permissions) and returns the Tutor's phrased response. No
    /// Assessment, no progression, no tool wiring yet.
    /// </summary>
    public async Task<TutorAgentResult> RespondAsync(
        TutorTurnContext context,
        CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "TutorAgent is not configured (missing Azure OpenAI settings).");
        }

        var prompt = BuildPrompt(context);

        var response = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);

        return new TutorAgentResult { Response = response.Text };
    }

    private static string BuildPrompt(TutorTurnContext context)
    {
        var evidenceSummary = context.AccumulatedEvidence.Count == 0
            ? "(ninguna todavía)"
            : string.Join(
                "\n",
                context.AccumulatedEvidence.Select(e =>
                    $"- [{e.Origin}] {string.Join(" | ", e.Parts.Select(p => p.Text ?? p.StorageUrl ?? "(sin contenido de texto)"))}"));

        var knowledgeNote = context.Permissions.KnowledgeAccessAllowed
            ? "Puedes fundamentar tu respuesta en el material del módulo."
            : "NO tienes acceso a material de referencia en este turno — el alumno debe recuperar/predecir por su cuenta.";

        // Paso 5 (2026-07-14): Skill guidance is Runtime-selected content
        // (TutorSkillSelector), never chosen by the Tutor itself — injected
        // directly into the turn prompt rather than through Harness's own
        // (experimental) AgentSkillsProvider mechanism. See
        // /memories/repo/human-os-runtime-design.md for the rationale.
        var skillGuidance = context.ActiveSkill is { } skill
            ? TutorSkillLibrary.InstructionsFor(skill)
            : "(sin skill pedagógica específica para esta etapa)";

        var lastAssessmentNote = BuildLastAssessmentNote(context);

        var moduleIntroNote = context.CurrentStage == RuntimeStage.ModuleStarted
            ? $"""

              Título del módulo: {context.Contract.ModuleTitle}
              Descripción del módulo: {context.Contract.ModuleDescription}
              Nota: primero vas a ENSEÑAR el contenido (etapa Instruction), y solo DESPUÉS
              se le pedirá al alumno que recuerde/prediga lo enseñado. En esta bienvenida,
              dile que a continuación viene el contenido a aprender y que preste atención,
              porque luego se le pedirá recordarlo sin volver a mirarlo. No le digas todavía
              que "está bien no saber la respuesta" — esa frase pertenece a la etapa de Recall,
              después de haber enseñado, no antes.

              """
            : string.Empty;

        var chapterNote = BuildChapterNote(context);
        var learnerTaskNote = BuildLearnerTaskNote(context);

        return $"""
            Etapa actual: {context.CurrentStage}

            Guía pedagógica para esta etapa:
            {skillGuidance}

            Contrato pedagógico del módulo:
            - Métrica objetivo: {context.Contract.TargetMetric}
            - Requisito de recuperación: {context.Contract.RecallRequirement}
            - Producción esperada del alumno: {context.Contract.LearnerProduction}
            - Criterios de éxito: {string.Join(" | ", context.Contract.SuccessCriteria)}
            {(context.CurrentStage == RuntimeStage.Instruction ? $"\n            Contenido del módulo a presentar (basa tu explicación en esto, no lo omitas):\n            {context.ModuleScript}\n" : string.Empty)}
            {chapterNote}
            {learnerTaskNote}
            {moduleIntroNote}
            Evidencia acumulada en esta sesión:
            {evidenceSummary}
            {lastAssessmentNote}
            {knowledgeNote}

            Genera el texto que el alumno debe ver ahora para esta etapa.
            """;
    }

    /// <summary>
    /// Renders the active chapter's relevant field as grounded source text
    /// for the 4 <c>Chapter*</c> stages (fixed 2026-07-16) — mirrors the
    /// Instruction-stage <c>ModuleScript</c> inlining above, but scoped to
    /// ONE chapter (<see cref="TutorTurnContext.CurrentChapterIndex"/>)
    /// instead of the whole script. Empty for every other stage.
    /// </summary>
    private static string BuildChapterNote(TutorTurnContext context)
    {
        if (context.CurrentChapterIndex is not { } chapterIndex ||
            chapterIndex < 0 || chapterIndex >= context.Contract.Chapters.Count)
        {
            return string.Empty;
        }

        var chapter = context.Contract.Chapters[chapterIndex];
        var position = $"Capítulo {chapterIndex + 1} de {context.Contract.Chapters.Count}: {chapter.Title}";

        var sourceText = context.ChapterSourceTextOverride ?? context.CurrentStage switch
        {
            RuntimeStage.ChapterTeaching => chapter.TeachingContent,
            RuntimeStage.ChapterRecall => chapter.RecallPrompt,
            RuntimeStage.ChapterPrediction => chapter.PredictionPrompt ?? string.Empty,
            RuntimeStage.ChapterMiniPractice => chapter.MiniPracticePrompt ?? string.Empty,
            _ => string.Empty
        };

        // Fixed 2026-07-17 (round 2 — the FIRST fix only split an
        // ALREADY multi-part stored prompt; it did nothing to stop the
        // Tutor from INVENTING its own numbered questionnaire when
        // phrasing a perfectly single-question source, which is exactly
        // what happened live: the user got a fresh wall of "1) ... 2) ..."
        // questions again even though the stored PredictionPrompt/segment
        // was one atomic question). For the two interactive back-and-
        // forth stages (Recall/Prediction), ALWAYS constrain the Tutor's
        // OWN generation to one short, natural, conversational question —
        // never a list/survey — regardless of how many angles the source
        // content suggests. This replaces the generic "no lo omitas, no
        // lo resumas" instruction for these two stages specifically,
        // since that wording was pushing the model toward exhaustively
        // covering every angle as a checklist instead of picking one.
        var isInteractiveQuestionStage = context.CurrentStage is
            RuntimeStage.ChapterRecall or RuntimeStage.ChapterPrediction;

        var contentInstruction = isInteractiveQuestionStage
            ? "Con base en esto, formula UNA SOLA pregunta corta y natural, como la haría un " +
              "tutor humano hablando en voz alta — NUNCA una lista numerada, NUNCA varios " +
              "sub-puntos o incisos (a, b, c...), NUNCA algo que el alumno deba \"responder por " +
              "partes\". Si el contenido de origen sugiere varios ángulos, elige el MÁS " +
              "importante y descarta el resto — no intentes cubrirlos todos en este turno."
            : "Basa tu texto en esto, no lo omitas, no lo resumas — el alumno solo verá lo que " +
              "tú generes ahora.";

        var dialogueNote = context.IsMultiPartChapterDialogueTurn
            ? "\nAdemás, esto es SOLO UNA pregunta de una serie de varias que se harán en turnos " +
              "separados — NO digas \"pregunta X de Y\", NO la enumeres, NO menciones que hay más " +
              "preguntas después; trátala como si fuera la única.\n"
            : string.Empty;

        return $"""

            {position}
            {dialogueNote}
            Contenido de origen de este capítulo ({contentInstruction}):
            {sourceText}

            """;
    }

    /// <summary>
    /// Grounds a LearnerProduction turn in the Instructor's REAL concrete
    /// task content (fixed 2026-07-17 — closes a real grounding gap found
    /// live: the Tutor was inventing the concrete exercise content, e.g.
    /// "las cinco expresiones", fresh every turn, with zero stored
    /// grounding — only the terse Arquitecto-level LearnerProduction
    /// description existed before this fix). Empty when
    /// <see cref="RuntimePedagogicalContract.LearnerTask"/> is empty
    /// (modules published before this fix).
    /// </summary>
    private static string BuildLearnerTaskNote(TutorTurnContext context)
    {
        if (context.CurrentStage != RuntimeStage.LearnerProduction ||
            string.IsNullOrWhiteSpace(context.Contract.LearnerTask))
        {
            return string.Empty;
        }

        var sourceText = context.LearnerTaskOverride ?? context.Contract.LearnerTask;

        var dialogueNote = context.IsMultiPartLearnerTaskTurn
            ? "\nEsto es SOLO UN ítem de una serie de varios que se presentarán en turnos " +
              "separados — NO digas \"ítem X de Y\", NO lo enumeres, NO menciones que hay más " +
              "ítems después; trátalo como si fuera la única tarea de este turno.\n"
            : string.Empty;

        return $"""

            Tarea concreta del alumno (basa tu instrucción en esto — el alumno solo verá lo
            que tú generes ahora, nunca reveles la solución):
            {dialogueNote}
            {sourceText}

            """;
    }

    /// <summary>
    /// Renders the previous Assessment's per-criterion verdict as an
    /// answer-free feedback note for a LearnerProduction retry prompt
    /// (fixed Paso 9, 2026-07-15) — empty when there is no prior
    /// Assessment, or when it was already Verified (no retry happening).
    /// </summary>
    private static string BuildLastAssessmentNote(TutorTurnContext context)
    {
        if (context.LastAssessment is not { } assessment ||
            assessment.Status == MetricVerificationStatus.Verified)
        {
            return string.Empty;
        }

        var criteriaNotes = string.Join(
            "\n",
            assessment.SuccessCriteriaResults.Select(c =>
                $"- [{(c.IsSatisfied ? "cumplido" : "NO cumplido")}] {c.Criterion}: {c.Evidence}"));

        return $"""

            Retroalimentación del intento anterior (NO reveles la respuesta,
            solo ayuda al alumno a ver qué le faltó):
            {criteriaNotes}

            """;
    }

    /// <summary>
    /// Verifies the module's <see cref="RuntimePedagogicalContract.TargetMetric"/>
    /// against the learner's real evidence (fixed Paso 6, 2026-07-14) —
    /// same LLM-proposes/code-decides shape as Studio's Métrico agent.
    /// The LLM's structured verdict is never trusted directly; it is
    /// always run through <see cref="HumanOS.Agentic.Runtime.RuntimeAssessmentValidator"/>
    /// first.
    /// </summary>
    public async Task<RuntimeAssessmentResult> AssessAsync(
        TutorTurnContext context,
        CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "TutorAgent is not configured (missing Azure OpenAI settings).");
        }

        var prompt = BuildAssessmentPrompt(context);

        var response = await _agent.RunAsync<RuntimeAssessmentResult>(prompt, cancellationToken: cancellationToken);
        var result = response.Result;

        RuntimeAssessmentValidator.Validate(context.Contract, result);

        return result;
    }

    private static string BuildAssessmentPrompt(TutorTurnContext context)
    {
        var evidenceText = context.AccumulatedEvidence.Count == 0
            ? "(ninguna evidencia registrada)"
            : string.Join(
                "\n",
                context.AccumulatedEvidence.Select(e =>
                    $"- [{e.Origin}] {string.Join(" | ", e.Parts.Select(p => p.Text ?? p.StorageUrl ?? "(sin contenido de texto)"))}"));

        var criteriaText = string.Join(
            "\n", context.Contract.SuccessCriteria.Select((c, i) => $"{i + 1}. {c}"));

        return $"""
            TargetMetric a verificar (repítelo exactamente): {context.Contract.TargetMetric}

            Criterios de éxito aprobados — evalúa TODOS, en este orden exacto
            ({context.Contract.SuccessCriteria.Count} en total):
            {criteriaText}

            Evidencia producida por el alumno en esta sesión:
            {evidenceText}

            Determina si el TargetMetric queda Verified, NotVerified o Failed,
            basándote ÚNICAMENTE en la evidencia anterior.
            """;
    }

    /// <summary>
    /// Lightweight completeness check for a single Recall attempt (fixed
    /// 2026-07-16 — implements iterative retrieval practice: "quiero que
    /// el usuario pueda repasar o aprender con varias iteraciones de
    /// preguntas y respuestas", grounded in "The Memory Paradox" (Oakley
    /// et al., 2025)). Deliberately separate from <see cref="AssessAsync"/>:
    /// this never claims Recall-as-metric is verified, it only decides
    /// whether to give the learner one more bounded retrieval attempt with
    /// an answer-free Socratic follow-up before moving on to Prediction.
    /// The Tutor DOES see the real teaching content here (needed to judge
    /// completeness) but is instructed to never leak it into
    /// <see cref="RecallCheckResult.FollowUpPrompt"/>.
    /// </summary>
    public async Task<RecallCheckResult> CheckRecallAsync(
        RuntimeSessionState state,
        CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "TutorAgent is not configured (missing Azure OpenAI settings).");
        }

        var latestRecall = LatestRecallEvidence(state);
        // Fixed 2026-07-17: prefer the Chapters' own clean TeachingContent
        // (never embeds Recall/Prediction/Restrictions instructions) over
        // the legacy whole ModuleScript when Chapters exist — comparing
        // against ModuleScript's embedded activity-instruction noise (e.g.
        // "Restricciones: No uses calculadoras...") made the completeness
        // judgment worse, not just the eventual reveal text.
        var contract = state.Session.Contract;
        var sourceContent = contract.Chapters.Count > 0
            ? string.Join("\n\n", contract.Chapters.Select(c => c.TeachingContent))
            : contract.ModuleScript;
        var prompt = BuildRecallCheckPrompt(sourceContent, latestRecall);
        var response = await _agent.RunAsync<RecallCheckResult>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }

    /// <summary>
    /// Same lightweight completeness check as <see cref="CheckRecallAsync"/>,
    /// but scoped to ONE chapter's own content (fixed 2026-07-16 — closes a
    /// real gap the product owner flagged: a chapter's Recall answer was
    /// silently advancing to the next chapter with ZERO feedback on
    /// whether it was right or wrong, "es ahi donde creo tenemos que
    /// llamar al agente pasarle el contexto del capitulo completo... y
    /// tenerlo en un pequeño loop... dandole tips hasta que aprenda").
    /// Compares against THIS chapter's <c>TeachingContent</c> only, not
    /// the whole module script — a chapter's recall must not be judged
    /// against material from other chapters the learner hasn't reached
    /// yet.
    /// </summary>
    public async Task<RecallCheckResult> CheckChapterRecallAsync(
        RuntimeSessionState state,
        int chapterIndex,
        CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "TutorAgent is not configured (missing Azure OpenAI settings).");
        }

        var chapter = state.Session.Contract.Chapters[chapterIndex];
        var latestRecall = LatestRecallEvidence(state);
        var prompt = BuildRecallCheckPrompt(chapter.TeachingContent, latestRecall);
        var response = await _agent.RunAsync<RecallCheckResult>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }

    private static string LatestRecallEvidence(RuntimeSessionState state) =>
        state.Session.Evidence
            .Where(e => e.Origin == StudentEvidenceOrigin.Recall)
            .Select(e => string.Join(" | ", e.Parts.Select(p => p.Text ?? p.StorageUrl ?? "(sin contenido de texto)")))
            .LastOrDefault() ?? "(sin evidencia)";

    private static string BuildRecallCheckPrompt(string sourceContent, string latestRecall)
    {
        return $"""
            Eres un evaluador interno — el alumno NUNCA ve este prompt ni tu
            razonamiento, solo el campo FollowUpPrompt si decides que hace
            falta otro intento. Tu única tarea: decidir si el intento de
            recuerdo del alumno es razonablemente completo comparado con el
            contenido real que se le enseñó, y si falta algo importante,
            redactar UNA pregunta o pista socrática que ayude al alumno a
            recuperar más por su cuenta — SIN revelar el dato faltante
            directamente. Nunca escribas la respuesta exacta en
            FollowUpPrompt; usa el estilo "error de predicción" (ej.
            "mencionaste varias etapas, ¿qué crees que pasa con la
            temperatura del agua?" en vez de revelar la temperatura).

            Contenido real enseñado (el alumno ya lo leyó, pero ahora intenta
            recordarlo sin volver a mirarlo):
            {sourceContent}

            Intento de recuerdo del alumno:
            {latestRecall}

            PRIMERO decide IsGenuineAttempt: márcalo false SOLO si lo que
            escribió el alumno NO es en realidad un intento de recordar —
            por ejemplo, es una pregunta aclaratoria ("¿qué significa
            normalizar?"), confusión sobre la tarea, o un comentario fuera
            de tema. Si es false, en FollowUpPrompt responde brevemente esa
            pregunta/confusión (sobre vocabulario o instrucciones, NUNCA
            revelando el contenido que debe recordar) y luego reinvita al
            alumno a intentar el recuerdo original de nuevo. Dejar
            AccuracyPercentage en 0 cuando IsGenuineAttempt es false.

            Si IsGenuineAttempt es true, marca IsSufficient = true cuando el
            intento cubre razonablemente las etapas/ideas principales (no
            necesita ser perfecto ni literal — el objetivo es retrieval
            practice genuino, no memorización exacta). Marca IsSufficient =
            false SOLO si omite algo claramente importante, y en ese caso
            escribe la pregunta de seguimiento en FollowUpPrompt. Si
            IsSufficient es true, deja FollowUpPrompt vacío. También estima
            AccuracyPercentage (0-100): qué tan completo/correcto fue el
            intento comparado con el contenido real — un número aproximado
            y honesto, no una nota formal, solo para que el alumno vea su
            propio progreso de un intento a otro.
            """;
    }
}
