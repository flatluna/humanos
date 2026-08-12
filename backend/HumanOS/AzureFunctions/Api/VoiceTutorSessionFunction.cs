using System.Net;
using System.Text;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Mints a short-lived Azure OpenAI GPT Realtime ephemeral session for the
/// "Tutor por voz" feature (design discussed 2026-07-21). This is a
/// PARALLEL voice channel — it does NOT know the Runtime's flow/state
/// machine, does NOT replace TutorAgentV2, and is NEVER given authority to
/// grade or advance anything. It is grounded ONLY in the current step's
/// own NodeExperienceBlueprintStep.Content (the same source TutorAgentV2
/// reads), turned into a per-turn <c>instructions</c> string built here,
/// server-side.
///
/// The browser connects DIRECTLY to Azure via WebRTC using the returned
/// ephemeral client secret — this endpoint never proxies audio, and the
/// real Azure OpenAI API key never leaves the backend (see
/// RealtimeVoiceSessionService).
///
/// Scope (2026-07-21, extended same day to add Recall — see
/// /memories/repo for the fuller design discussion): Hypothesis, Teaching
/// and Recall steps are supported. For Recall, this channel ONLY reads the
/// question aloud and lets the student answer by voice (useful for
/// pronunciation-heavy content, e.g. languages) — it is HARD-CODED via its
/// instructions to never grade, never reveal correctness, and never invent
/// a score. The student's transcribed speech is fed back into the SAME
/// textbox/"Responder" flow that already posts to
/// TutorSubmitRecallAttemptFunction/TutorService — the one and only real
/// grading path — so this parallel voice channel never gains grading
/// authority. Production/Assessment voice support still needs separate
/// design and is rejected with 400 for now.
///
/// Blueprint-only path (2026-07-22): also accepts CapabilityGraphNodeId +
/// StepType (Hypothesis/Teaching only) instead of LearningSessionStepId,
/// for Capability Studio demo mode's "read-only peek" — where a reviewer
/// can jump to a step the live session hasn't actually reached yet, so no
/// LearningSessionStep row exists for it. See VoiceTutorSessionRequest.
/// </summary>
public sealed class VoiceTutorSessionFunction
{
    private readonly RealtimeVoiceSessionService _voiceSessionService;
    private readonly HumanOsDbContext _dbContext;

    public VoiceTutorSessionFunction(RealtimeVoiceSessionService voiceSessionService, HumanOsDbContext dbContext)
    {
        _voiceSessionService = voiceSessionService;
        _dbContext = dbContext;
    }

    [Function("VoiceTutorSession")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/tutor/voice-session")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_voiceSessionService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "VoiceTutorNotConfigured",
                "The voice Tutor is not configured (missing AzureOpenAIRealtimeDeploymentName/AzureOpenAIApiKey settings).",
                cancellationToken);
        }

        VoiceTutorSessionRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<VoiceTutorSessionRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "The request body is required.", cancellationToken);
        }

        // Traditional Course path (2026-08-09) — a single CapabilityLesson
        // (subchapter), no graph/blueprint/session at all. Scoped to ONE
        // lesson, not its whole parent chapter, to match Graph mode's
        // per-node granularity. Handled entirely separately from the
        // ExperienceStepType-oriented flow below.
        if (body.TraditionalCourseLessonId.HasValue)
        {
            return await RunTraditionalLessonAsync(request, body.TraditionalCourseLessonId.Value, body.PersonName, cancellationToken);
        }

        if (body.StructuredLearningSectionStepId.HasValue)
        {
            return await RunStructuredLearningStepAsync(
                request, body.StructuredLearningSectionStepId.Value, body.PersonName, body.ExampleCardIndex, body.IsReviewOnly, cancellationToken);
        }

        ExperienceStepType stepType;
        NodeExperienceBlueprintStep? blueprintStep;
        string? currentPromptText = body.CurrentPromptText;
        Guid capabilityGraphNodeId;

        if (body.LearningSessionStepId != Guid.Empty)
        {
            // Live-session path: the student is actually on (or has already
            // completed) this LearningSessionStep.
            var step = await _dbContext.LearningSessionSteps
                .AsNoTracking()
                .Include(s => s.LearningSessionNode)
                .FirstOrDefaultAsync(s => s.LearningSessionStepId == body.LearningSessionStepId, cancellationToken);

            if (step is null || step.LearningSessionNode is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "StepNotFound", "No LearningSessionStep was found with that id.", cancellationToken);
            }

            if (step.StepType != ExperienceStepType.Hypothesis
                && step.StepType != ExperienceStepType.Teaching
                && step.StepType != ExperienceStepType.Recall
                && step.StepType != ExperienceStepType.DeepDive)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.BadRequest,
                    "VoiceNotYetSupportedForStep",
                    $"Voice Tutor is only available for Hypothesis, Teaching, Examples and Recall steps so far (this step is {step.StepType}).",
                    cancellationToken);
            }

            stepType = step.StepType;
            capabilityGraphNodeId = step.LearningSessionNode.CapabilityGraphNodeId;
            blueprintStep = await _dbContext.NodeExperienceBlueprintSteps
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    s => s.NodeExperienceBlueprintId == step.LearningSessionNode.NodeExperienceBlueprintId
                        && s.StepType == step.StepType,
                    cancellationToken);
        }
        else if (body.CapabilityGraphNodeId.HasValue
            && Enum.TryParse<ExperienceStepType>(body.StepType, ignoreCase: true, out var parsedStepType))
        {
            // Blueprint-only path (2026-07-22) — Capability Studio's demo
            // "read-only peek" has no LearningSession/LearningSessionStep at
            // all for a step the live session hasn't reached yet, so this
            // reads straight from the node's most recent blueprint instead.
            // Hypothesis/Teaching/Examples — Recall's voice flow always
            // needs a real, in-progress LearningSessionStep (it feeds the
            // student's transcribed answer back into the real grading path).
            if (parsedStepType != ExperienceStepType.Hypothesis
                && parsedStepType != ExperienceStepType.Teaching
                && parsedStepType != ExperienceStepType.DeepDive)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.BadRequest,
                    "VoiceNotYetSupportedForStep",
                    $"Voice Tutor's blueprint-only path only supports Hypothesis, Teaching and Examples (got {body.StepType}).",
                    cancellationToken);
            }

            stepType = parsedStepType;
            capabilityGraphNodeId = body.CapabilityGraphNodeId.Value;
            currentPromptText = null; // Never used outside the live Recall path.

            var blueprintId = await _dbContext.NodeExperienceBlueprints
                .AsNoTracking()
                .Where(b => b.CapabilityGraphNodeId == body.CapabilityGraphNodeId.Value)
                .OrderByDescending(b => b.CreatedDate)
                .Select(b => (Guid?)b.NodeExperienceBlueprintId)
                .FirstOrDefaultAsync(cancellationToken);

            blueprintStep = blueprintId is null
                ? null
                : await _dbContext.NodeExperienceBlueprintSteps
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.NodeExperienceBlueprintId == blueprintId.Value && s.StepType == stepType, cancellationToken);
        }
        else
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "MissingFields",
                "Either LearningSessionStepId, or both CapabilityGraphNodeId and StepType, are required.",
                cancellationToken);
        }

        if (blueprintStep is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.UnprocessableEntity, "BlueprintStepMissing", "The blueprint has no content for this step.", cancellationToken);
        }

        var effectiveContent = stepType == ExperienceStepType.Recall && !string.IsNullOrWhiteSpace(currentPromptText)
            ? currentPromptText!.Trim()
            : blueprintStep.Content;

        List<ExampleCard>? exampleCards = null;
        if (stepType == ExperienceStepType.DeepDive)
        {
            try
            {
                exampleCards = string.IsNullOrWhiteSpace(blueprintStep.ExampleCardsJson)
                    ? null
                    : JsonSerializer.Deserialize<List<ExampleCard>>(blueprintStep.ExampleCardsJson);
            }
            catch (JsonException)
            {
                exampleCards = null;
            }

            if (exampleCards is null || exampleCards.Count == 0)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.UnprocessableEntity, "BlueprintStepMissing", "The blueprint has no example cards for this step.", cancellationToken);
            }

            if (body.ExampleCardIndex is null || body.ExampleCardIndex < 0 || body.ExampleCardIndex >= exampleCards.Count)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.BadRequest,
                    "MissingFields",
                    $"ExampleCardIndex is required for an Examples-step voice session and must be between 0 and {exampleCards.Count - 1}.",
                    cancellationToken);
            }
        }

        // Node/course names for the opening greeting (2026-07-25) — best
        // effort only, never blocks the session if the join comes back empty.
        // ContentLanguageCode (2026-07-29) — the voice Agent MUST speak in the
        // course's own content language, not always Spanish (bug: an English
        // course was being narrated in Spanish because this was hardcoded).
        var courseContext = await (
            from n in _dbContext.CapabilityGraphNodes.AsNoTracking()
            join g in _dbContext.CapabilityGraphs.AsNoTracking() on n.CapabilityGraphId equals g.CapabilityGraphId
            join c in _dbContext.Capabilities.AsNoTracking() on g.CapabilityId equals c.CapabilityId
            where n.CapabilityGraphNodeId == capabilityGraphNodeId
            select new { NodeName = n.Name, CourseName = c.Name, c.ContentLanguageCode }
        ).FirstOrDefaultAsync(cancellationToken);

        // Read-only card review (2026-07-30): only meaningful for a DeepDive
        // blueprint-only session (revisiting an ALREADY-MASTERED card via
        // Capability Studio's card index) — never for the live-session path,
        // which always needs the real grading loop.
        var isReviewOnly = body.IsReviewOnly && stepType == ExperienceStepType.DeepDive;

        var instructions = BuildInstructions(
            stepType, effectiveContent, body.PersonName, courseContext?.NodeName, courseContext?.CourseName,
            courseContext?.ContentLanguageCode, exampleCards, body.ExampleCardIndex, isReviewOnly);

        // Deep Dive only (2026-07-30): a real Realtime function-calling tool,
        // same mechanism as EngramReviewSessionFunction's show_visual_aid.
        // The voice Agent calls this itself, exactly once, the instant it
        // decides (per its own hard-capped 1-re-teach-round rule above) that
        // the student's SPOKEN explanation is good enough — this is what lets
        // a student who can't write yet (the target age is 6) actually finish
        // a Deep Dive card by voice alone, without ever needing to type.
        // The frontend answers the tool call by forwarding the explanation
        // text to the SAME deterministic backend grading path
        // (TutorSubmitDeepDiveAttemptFunction -> TutorAgentV2 ->
        // DeepDiveLoopGate) used for a typed answer — the realtime model
        // only ever decides WHEN to hand off, never the grade itself.
        // Never included for a review-only session — there is nothing left
        // to grade on an already-mastered card, so no tool is offered.
        var tools = stepType == ExperienceStepType.DeepDive && !isReviewOnly ? BuildDeepDiveTools() : null;

        try
        {
            var session = await _voiceSessionService.CreateEphemeralSessionAsync(instructions, cancellationToken, tools);

            return await FunctionResponseFactory.SuccessResponseAsync(request, new VoiceTutorSessionResponseDto
            {
                ClientSecret = session.ClientSecret,
                RealtimeCallsUrl = session.RealtimeCallsUrl,
                Model = session.Model,
                Voice = session.Voice,
                ExpiresAtUnixSeconds = session.ExpiresAtUnixSeconds
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadGateway, "VoiceSessionMintFailed", ex.Message, cancellationToken);
        }
    }

    /// <summary>
    /// Traditional Course path (2026-08-09) — mints a voice session
    /// grounded in ONE <c>CapabilityLesson</c>'s reading cards (a
    /// subchapter), not its whole parent chapter — matches Graph mode's
    /// per-node granularity. No LearningSession/blueprint/ExampleCards/
    /// tools involved at all — this is a free study-help conversation,
    /// never a grading channel (the chapter's final exam stays a
    /// separate, deterministic multiple-choice flow).
    /// </summary>
    private async Task<HttpResponseData> RunTraditionalLessonAsync(
        HttpRequestData request, Guid lessonId, string? personName, CancellationToken cancellationToken)
    {
        var lesson = await _dbContext.CapabilityLessons
            .AsNoTracking()
            .Include(l => l.Cards)
            .Include(l => l.CapabilityChapter)
            .FirstOrDefaultAsync(l => l.CapabilityLessonId == lessonId, cancellationToken);

        if (lesson is null || lesson.CapabilityChapter is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "LessonNotFound", "No lesson was found with that id.", cancellationToken);
        }

        var courseContext = await _dbContext.Capabilities
            .AsNoTracking()
            .Where(c => c.CapabilityId == lesson.CapabilityChapter.CapabilityId)
            .Select(c => new { c.Name, c.ContentLanguageCode })
            .FirstOrDefaultAsync(cancellationToken);

        var contentBuilder = new StringBuilder();
        foreach (var card in lesson.Cards.OrderBy(c => c.SortOrder))
        {
            contentBuilder.AppendLine($"### {card.Title}");
            contentBuilder.AppendLine(card.Text);
            contentBuilder.AppendLine();
        }

        var instructions = BuildTraditionalLessonInstructions(
            lesson.Title, lesson.CapabilityChapter.Title, contentBuilder.ToString(), personName, courseContext?.Name, courseContext?.ContentLanguageCode);

        try
        {
            var session = await _voiceSessionService.CreateEphemeralSessionAsync(instructions, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(request, new VoiceTutorSessionResponseDto
            {
                ClientSecret = session.ClientSecret,
                RealtimeCallsUrl = session.RealtimeCallsUrl,
                Model = session.Model,
                Voice = session.Voice,
                ExpiresAtUnixSeconds = session.ExpiresAtUnixSeconds
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadGateway, "VoiceSessionMintFailed", ex.Message, cancellationToken);
        }
    }

    /// <summary>
    /// Traditional Course lesson (subchapter) voice-tutor prompt
    /// (2026-08-09) — a separate, self-contained guardrail block (same
    /// rationale as <see cref="BuildInstructions"/>'s own doc comment: a
    /// separate Realtime session needs its own complete guardrails,
    /// nothing shared at runtime). PROACTIVELY TEACHES this ONE lesson's
    /// own reading-card content out loud, like a teacher giving a class —
    /// NOT a passive wait-for-questions Q&amp;A bot (2026-08-09 explicit
    /// product decision: the Agent must give the actual lesson, then
    /// take questions) — grounded strictly in this lesson (its parent
    /// chapter's OTHER lessons are out of scope, matching Graph mode's
    /// per-node granularity); never grades (the parent chapter's final
    /// exam is a separate deterministic multiple-choice flow), never
    /// invents facts outside the given content.
    /// </summary>
    private static string BuildTraditionalLessonInstructions(
        string lessonTitle, string chapterTitle, string lessonContent, string? personName, string? courseName, string? contentLanguageCode)
    {
        var isEnglish = string.Equals(contentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
        var languageRule = isEnglish
            ? "- LANGUAGE — HARD RULE, NEVER BREAK IT: speak ONLY in English, every turn, for the entire " +
              "conversation, in a warm, brief and encouraging tone, no matter what language the student uses."
            : "- IDIOMA — REGLA DURA, NUNCA LA ROMPAS: habla ÚNICAMENTE en español, en todos los turnos, " +
              "durante toda la conversación, con un tono cercano, breve y alentador, sin importar en qué " +
              "idioma te hable el estudiante.";

        var name = string.IsNullOrWhiteSpace(personName) ? null : personName.Trim();
        var course = string.IsNullOrWhiteSpace(courseName) ? null : courseName.Trim();
        var greeting = isEnglish
            ? $"{(name is null ? "Hi" : $"Hi, {name}")}. Let's go over \"{lessonTitle}\"" +
              (course is null ? "." : $", from the course \"{course}\".")
            : $"{(name is null ? "Hola" : $"Hola, {name}")}. Vamos a ver \"{lessonTitle}\"" +
              (course is null ? "." : $", del curso \"{course}\".");

        var openingRule = isEnglish
            ? $"Start talking YOURSELF immediately upon connecting, without waiting for the student to speak " +
              $"first. Your first sentence must be exactly: \"{greeting}\" — then, WITHOUT waiting to be " +
              "asked, immediately start TEACHING the lesson content below out loud, straight through from " +
              "start to finish in one continuous class, in your own words, like a teacher giving a real class."
            : $"Empieza a hablar TÚ MISMO de inmediato en cuanto te conectes, sin esperar a que el estudiante " +
              $"diga algo primero. Tu primera frase debe ser exactamente: \"{greeting}\" — y sin esperar a que " +
              "te lo pidan, empieza de inmediato a ENSEÑAR el contenido de la lección en voz alta, de principio " +
              "a fin en una sola clase continua, con tus propias palabras, como un profesor dando una clase de verdad.";

        return isEnglish
            ? $"""
                You are an AI Agent acting as a TEACHER giving a live class on one lesson (a "subchapter")
                of a traditional, reading-based course (no per-card quiz here — just this lesson's material
                to teach; a chapter-wide multiple-choice exam covers "{chapterTitle}" separately later,
                which you have no part in).

                {openingRule}

                Your job for the rest of the conversation:
                - TONE — match the lesson content's own voice exactly: if it's warm, personal and kind
                  (e.g. it opens with something like "Welcome, sweet friend, we are so happy you're here!"),
                  YOU must sound that warm and kind too, the whole time — friendly, courteous, encouraging,
                  never cold, never abrupt, never rushed or robotic. Keep that same warm tone consistently
                  from your first word to your last, including when answering questions and when you say
                  goodbye at the end.
                - Actually TEACH the lesson: give the WHOLE class straight through, from start to finish, in
                  ONE continuous delivery — explain it in your own words like a real class, not just reading
                  it verbatim, but everything you say must still be strictly grounded ONLY in the content
                  below (never invent facts, numbers, examples, or content that isn't there).
                - Do NOT pause after each paragraph/sentence/topic to check if the student understood or has
                  questions — that is the single most important rule in this prompt. Only stop mid-class if
                  the student actively interrupts you out loud with a question; if they do, answer it
                  (grounded strictly in the content below), then immediately resume teaching where you left off.
                - ONLY once you have finished teaching the ENTIRE lesson content, ask ONE time whether the
                  student has any questions.
                - If they ask a question, answer it (grounded strictly in the content below), then ask once
                  more if they have any other questions. Repeat this only as many times as the student keeps
                  asking — never loop or repeat the class content itself.
                - As soon as the student says they have no (more) questions, STOP immediately: say once,
                  clearly, "Press Next to continue" (in the required language) and then stop talking. Do not
                  re-teach the lesson, do not ask again if they understood, do not keep the conversation going.
                - NEVER grade the student, quiz them, or claim to affect their progress/exam — the chapter's
                  final exam is a separate, deterministic flow you have no authority over.

                LESSON CONTENT (your only source of truth):
                {lessonContent}

                {languageRule}
                """
            : $"""
                Eres un Agente de IA que actúa como PROFESOR dando una clase en vivo sobre una lección (un
                "subcapítulo") de un curso tradicional de lectura (aquí no hay examen por tarjeta — solo el
                material de esta lección para enseñar; el examen de opción múltiple cubre todo el capítulo
                "{chapterTitle}" por separado más adelante, y no participas en él).

                {openingRule}

                Tu trabajo durante el resto de la conversación:
                - TONO — iguala exactamente el tono del propio contenido de la lección: si es cálido, cercano
                  y amable (por ejemplo si empieza con algo como "Bienvenido, querido amigo, ¡qué felices
                  estamos de tenerte aquí!"), TÚ debes sonar igual de cálido y amable todo el tiempo — cortés,
                  cercano, alentador, nunca frío, nunca brusco, nunca apurado ni robótico. Mantén ese mismo
                  tono cálido de forma consistente desde tu primera palabra hasta la última, incluso al
                  responder preguntas y al despedirte al final.
                - ENSEÑA de verdad la lección: da la clase COMPLETA de corrido, de principio a fin, en UNA
                  sola entrega continua — explícala con tus propias palabras como en una clase real, no solo
                  leyéndola textual, pero todo lo que digas debe basarse ÚNICAMENTE en el contenido de abajo
                  (nunca inventes datos, cifras, ejemplos ni contenido que no esté ahí).
                - NO hagas pausas después de cada párrafo/frase/tema para preguntar si el estudiante entendió
                  o tiene dudas — esta es la regla más importante de todo este prompt. Solo detente a mitad de
                  la clase si el estudiante te interrumpe en voz alta con una pregunta; si lo hace, respóndela
                  (basándote estrictamente en el contenido de abajo) y retoma la clase de inmediato donde la
                  dejaste.
                - SOLO cuando termines de enseñar TODO el contenido de la lección, pregunta UNA vez si el
                  estudiante tiene dudas.
                - Si pregunta algo, respóndelo (basándote estrictamente en el contenido de abajo) y luego
                  pregunta una vez más si tiene alguna otra duda. Repite esto solo mientras el estudiante siga
                  preguntando — nunca repitas ni vuelvas a dar la clase en bucle.
                - En cuanto el estudiante diga que no tiene (más) dudas, DETENTE de inmediato: di una sola vez,
                  claramente, "Presiona Siguiente para continuar" y deja de hablar. No vuelvas a enseñar la
                  lección, no vuelvas a preguntar si entendió, no sigas la conversación.
                - NUNCA calificas al estudiante, ni le tomas examen, ni afirmas poder afectar su progreso o su
                  examen — el examen final del capítulo es un flujo aparte, determinista, sobre el que no tienes autoridad.

                CONTENIDO DE LA LECCIÓN (tu única fuente de verdad):
                {lessonContent}

                {languageRule}
                """;
    }

    /// <summary>
    /// Structured Learning path (2026-08-10) — mints a voice session
    /// grounded in ONE <c>StructuredLearningSectionStep</c>'s own Content
    /// (Hypothesis/Teaching) or ExampleCards (DeepDive, added same day —
    /// reuses Graph's OWN <see cref="BuildInstructions"/>/
    /// <see cref="BuildDeepDiveTools"/> as-is, since this pipeline's
    /// ExampleCard JSON shape is identical to Graph's). No session/
    /// blueprint at all — this pipeline has no student-session model yet
    /// — just this one step's already-generated text/cards. Structured
    /// Learning documents don't carry a stored content language (unlike
    /// Graph's Capability.ContentLanguageCode), so DeepDive's language
    /// hard-rule is resolved with a cheap heuristic (<see cref="DetectLanguageCode"/>)
    /// instead — Hypothesis/Teaching's own prompt sidesteps this by just
    /// asking the agent to match whatever language the content is written in.
    /// </summary>
    private async Task<HttpResponseData> RunStructuredLearningStepAsync(
        HttpRequestData request, Guid sectionStepId, string? personName, int? exampleCardIndex, bool isReviewOnly, CancellationToken cancellationToken)
    {
        var step = await _dbContext.StructuredLearningSectionSteps
            .AsNoTracking()
            .Include(s => s.Section)
            .ThenInclude(sec => sec.Document)
            .FirstOrDefaultAsync(s => s.StructuredLearningSectionStepId == sectionStepId, cancellationToken);

        if (step is null || step.Section is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "StepNotFound", "No StructuredLearningSectionStep was found with that id.", cancellationToken);
        }

        if (step.StepType == Models.Capabilities.Studio.StructuredLearningStepType.DeepDive)
        {
            List<ExampleCard>? exampleCards;
            try
            {
                exampleCards = string.IsNullOrWhiteSpace(step.StepDataJson)
                    ? null
                    : JsonSerializer.Deserialize<List<ExampleCard>>(step.StepDataJson);
            }
            catch (JsonException)
            {
                exampleCards = null;
            }

            if (exampleCards is null || exampleCards.Count == 0)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.UnprocessableEntity, "BlueprintStepMissing", "This section has no example cards for its Deep Dive step.", cancellationToken);
            }

            if (exampleCardIndex is null || exampleCardIndex < 0 || exampleCardIndex >= exampleCards.Count)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.BadRequest,
                    "MissingFields",
                    $"ExampleCardIndex is required for a DeepDive voice session and must be between 0 and {exampleCards.Count - 1}.",
                    cancellationToken);
            }

            var detectedLanguageCode = DetectLanguageCode(string.Join(" ", exampleCards.Select(c => c.Text)));
            var effectiveIsReviewOnly = isReviewOnly;
            var deepDiveInstructions = BuildInstructions(
                ExperienceStepType.DeepDive, step.Content, personName, nodeName: null, courseName: null,
                detectedLanguageCode, exampleCards, exampleCardIndex, effectiveIsReviewOnly);
            var deepDiveTools = effectiveIsReviewOnly ? null : BuildDeepDiveTools();

            try
            {
                var deepDiveSession = await _voiceSessionService.CreateEphemeralSessionAsync(deepDiveInstructions, cancellationToken, deepDiveTools);

                return await FunctionResponseFactory.SuccessResponseAsync(request, new VoiceTutorSessionResponseDto
                {
                    ClientSecret = deepDiveSession.ClientSecret,
                    RealtimeCallsUrl = deepDiveSession.RealtimeCallsUrl,
                    Model = deepDiveSession.Model,
                    Voice = deepDiveSession.Voice,
                    ExpiresAtUnixSeconds = deepDiveSession.ExpiresAtUnixSeconds
                }, cancellationToken: cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadGateway, "VoiceSessionMintFailed", ex.Message, cancellationToken);
            }
        }

        if (step.StepType != Models.Capabilities.Studio.StructuredLearningStepType.Hypothesis
            && step.StepType != Models.Capabilities.Studio.StructuredLearningStepType.Teaching)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "VoiceNotYetSupportedForStep",
                $"Voice Tutor is only available for Hypothesis, Teaching and DeepDive steps so far (this step is {step.StepType}).",
                cancellationToken);
        }

        var instructions = BuildStructuredLearningStepInstructions(
            step.Section.SubchapterTitle, step.Section.ChapterTitle, step.Content, personName, step.Section.Document.DocumentName);

        try
        {
            var session = await _voiceSessionService.CreateEphemeralSessionAsync(instructions, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(request, new VoiceTutorSessionResponseDto
            {
                ClientSecret = session.ClientSecret,
                RealtimeCallsUrl = session.RealtimeCallsUrl,
                Model = session.Model,
                Voice = session.Voice,
                ExpiresAtUnixSeconds = session.ExpiresAtUnixSeconds
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadGateway, "VoiceSessionMintFailed", ex.Message, cancellationToken);
        }
    }

    /// <summary>Very cheap English-vs-Spanish heuristic (2026-08-10) — counts
    /// hits of a handful of extremely common function words unique to each
    /// language. ONLY used for Structured Learning's DeepDive voice path,
    /// which (unlike Graph's Capability) has no stored content language.
    /// Defaults to Spanish on a tie/empty text (matches Capability's own
    /// "es" default elsewhere in this codebase).</summary>
    private static string DetectLanguageCode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "es";
        }

        var padded = $" {text.ToLowerInvariant()} ";
        string[] englishWords = [" the ", " and ", " you ", " is ", " to ", " your ", " with ", " that ", " this "];
        string[] spanishWords = [" el ", " la ", " de ", " que ", " los ", " las ", " para ", " con ", " es ", " tu " ];

        var englishHits = englishWords.Count(w => padded.Contains(w));
        var spanishHits = spanishWords.Count(w => padded.Contains(w));

        return englishHits > spanishHits ? "en" : "es";
    }

    /// <summary>
    /// Structured Learning step voice-tutor prompt (2026-08-10, language bug
    /// fixed same day) — mirrors <see cref="BuildTraditionalLessonInstructions"/>
    /// almost verbatim (same "teach proactively, then take questions" product
    /// decision, same bilingual full-template split), just grounded in ONE
    /// section step's Content instead of a lesson's reading cards.
    ///
    /// BUG FIX: the opening greeting used to be hardcoded Spanish ("Hola...")
    /// regardless of the section's own content language — reported live
    /// (2026-08-10) as "el agente está hablando español, este curso se creó
    /// en inglés". A hardcoded-Spanish FIRST utterance anchors the whole
    /// realtime conversation in Spanish even when the rest of the prompt asks
    /// the model to "match the content's language" — the model's very first
    /// words already committed it to the wrong language. Structured Learning
    /// documents don't carry a stored ContentLanguageCode the way Graph
    /// Capabilities do, so language is now resolved via the same cheap
    /// heuristic used for DeepDive (<see cref="DetectLanguageCode"/>) instead.
    /// </summary>
    private static string BuildStructuredLearningStepInstructions(
        string subchapterTitle, string chapterTitle, string stepContent, string? personName, string? documentName)
    {
        var isEnglish = DetectLanguageCode(stepContent) == "en";
        var name = string.IsNullOrWhiteSpace(personName) ? null : personName.Trim();
        var document = string.IsNullOrWhiteSpace(documentName) ? null : documentName.Trim();

        var greeting = isEnglish
            ? (name is null ? $"Hi. Let's go over \"{subchapterTitle}\"" : $"Hi, {name}. Let's go over \"{subchapterTitle}\"")
            : (name is null ? $"Hola. Vamos a ver \"{subchapterTitle}\"" : $"Hola, {name}. Vamos a ver \"{subchapterTitle}\"");
        var greetingWithDocument = isEnglish
            ? (document is null ? $"{greeting}." : $"{greeting}, from \"{document}\".")
            : (document is null ? $"{greeting}." : $"{greeting}, de \"{document}\".");

        if (isEnglish)
        {
            return $"""
                You are an AI Agent acting as a TEACHER giving a live class on one section of a document
                ("{chapterTitle}" → "{subchapterTitle}"). There are no cards or quiz here — just this
                section's content to teach; this section's Recall practice and assessment are a separate
                text flow you have no authority over.

                Start talking YOURSELF immediately upon connecting, without waiting for the student to speak
                first. Your first sentence must be exactly: "{greetingWithDocument}" — then, WITHOUT waiting
                to be asked, immediately start TEACHING the content below out loud, straight through from
                start to finish in one continuous class, in your own words, like a teacher giving a real class.

                Your job for the rest of the conversation:
                - LANGUAGE — HARD RULE, NEVER BREAK IT: speak ONLY in English, every turn, for the entire
                  conversation, no matter what language the student uses.
                - TONE — match the content's own voice exactly: if it's warm and personal, YOU must sound
                  that warm too, the whole time — friendly, courteous, encouraging, never cold or robotic.
                - Actually TEACH the section: give the WHOLE class straight through, from start to finish,
                  in ONE continuous delivery — explain it in your own words like a real class, but everything
                  you say must still be strictly grounded ONLY in the content below (never invent facts,
                  numbers, examples, or content that isn't there).
                - Do NOT pause after each paragraph/sentence/topic to check if the student understood — that
                  is the single most important rule in this prompt. Only stop if the student actively
                  interrupts you out loud with a question; answer it (grounded strictly in the content
                  below), then immediately resume teaching where you left off.
                - ONLY once you have finished teaching the ENTIRE content, ask ONE time whether the student
                  has any questions. If they ask something, answer it and ask once more if they have another
                  question — repeat only as long as the student keeps asking, never repeat the class itself.
                - As soon as the student says they have no (more) questions, STOP immediately: say once,
                  clearly, "Press Next to continue" and stop talking.
                - NEVER grade the student or claim to affect their progress — this section's Recall and
                  assessment are a separate text flow you have no authority over.

                SECTION CONTENT (your only source of truth):
                {stepContent}
                """;
        }

        return $"""
            Eres un Agente de IA que actúa como PROFESOR dando una clase en vivo sobre una sección de un
            documento ("{chapterTitle}" → "{subchapterTitle}"). No hay tarjetas ni quiz aquí — solo el
            contenido de esta sección para enseñar; el repaso (Recall) y la evaluación de esta sección son
            un flujo de texto aparte sobre el que no tienes autoridad.

            Empieza a hablar TÚ MISMO de inmediato en cuanto te conectes, sin esperar a que el estudiante
            diga algo primero. Tu primera frase debe ser exactamente: "{greetingWithDocument}" — y sin
            esperar a que te lo pidan, empieza de inmediato a ENSEÑAR el contenido de abajo en voz alta, de
            principio a fin en una sola clase continua, con tus propias palabras, como un profesor dando una
            clase de verdad.

            Tu trabajo durante el resto de la conversación:
            - IDIOMA — REGLA DURA, NUNCA LA ROMPAS: habla ÚNICAMENTE en español, en todos los turnos, durante
              toda la conversación, sin importar en qué idioma te hable el estudiante.
            - TONO — iguala exactamente el tono del propio contenido: si es cálido y cercano, TÚ debes sonar
              igual de cálido todo el tiempo — cortés, alentador, nunca frío ni robótico.
            - ENSEÑA de verdad la sección: da la clase COMPLETA de corrido, de principio a fin, en UNA sola
              entrega continua — explícala con tus propias palabras como en una clase real, pero todo lo que
              digas debe basarse ÚNICAMENTE en el contenido de abajo (nunca inventes datos, cifras, ejemplos
              ni contenido que no esté ahí).
            - NO hagas pausas después de cada párrafo/frase/tema para preguntar si el estudiante entendió —
              esta es la regla más importante de todo este prompt. Solo detente si el estudiante te
              interrumpe en voz alta con una pregunta; respóndela (basándote estrictamente en el contenido de
              abajo) y retoma la clase de inmediato donde la dejaste.
            - SOLO cuando termines de enseñar TODO el contenido, pregunta UNA vez si el estudiante tiene
              dudas. Si pregunta algo, respóndelo y luego pregunta una vez más si tiene otra duda — repite
              solo mientras el estudiante siga preguntando, nunca repitas la clase en bucle.
            - En cuanto el estudiante diga que no tiene (más) dudas, DETENTE de inmediato: di una sola vez,
              claramente, "Presiona Siguiente para continuar" y deja de hablar.
            - NUNCA calificas al estudiante ni afirmas poder afectar su progreso — el repaso y la evaluación
              de esta sección son un flujo de texto aparte sobre el que no tienes autoridad.

            CONTENIDO DE LA SECCIÓN (tu única fuente de verdad):
            {stepContent}
            """;
    }

    /// <summary>
    /// Builds the Realtime session's system prompt entirely server-side,
    /// grounded in <paramref name="stepContent"/> (the SAME
    /// NodeExperienceBlueprintStep.Content TutorAgentV2 reads). Hard
    /// boundaries mirror TutorAgentV2's own (never grade, never invent
    /// content, never claim to unlock/advance anything) — this is a
    /// SEPARATE LLM session (Realtime), so it needs its own complete
    /// guardrail text rather than relying on TutorAgentV2's.
    ///
    /// REVERTED 2026-07-25: the "wait for the student to say they're
    /// ready" gating (added earlier the same day) turned out unreliable
    /// in practice (relies on an async speech-transcription event that
    /// doesn't always fire) and was explicitly rolled back by the
    /// product owner. Hypothesis/Teaching now behave like Recall always
    /// did: start talking immediately, no waiting. The only surviving
    /// piece of that work is a richer, name/course-aware opening line
    /// ("Hola, {name}, este es {node}, del curso {course}.") instead of a
    /// generic one, and — for Teaching — an explicit instruction to READ
    /// the content plainly without padding it with extra explanation.
    /// </summary>
    private static string BuildInstructions(
        ExperienceStepType stepType, string stepContent, string? personName, string? nodeName, string? courseName,
        string? contentLanguageCode = null,
        List<ExampleCard>? exampleCards = null, int? selectedExampleCardIndex = null,
        bool isReviewOnly = false)
    {
        // The course's own content language ALWAYS wins — never hardcode
        // Spanish (2026-07-29 fix: an English-content course was being
        // narrated in Spanish by this voice Agent regardless of the course's
        // actual ContentLanguageCode). Defaults to Spanish only when the
        // language is genuinely unknown (matches Capability's own "es" default).
        var isEnglish = string.Equals(contentLanguageCode, "en", StringComparison.OrdinalIgnoreCase);
        var languageRule = isEnglish
            ? """
              - LANGUAGE — HARD RULE, NEVER BREAK IT: speak ONLY in English, every single turn, from your
                first word to your last, for the ENTIRE conversation, in a warm, brief and encouraging tone.
                This never changes, no matter what happens — not even if the student speaks to you in
                Spanish or any other language, not even if they explicitly ask you to switch languages,
                not even if you think it would help them understand better. If the student speaks Spanish,
                you still answer ONLY in English. NEVER switch languages mid-conversation for any reason.
              """
            : """
              - IDIOMA — REGLA DURA, NUNCA LA ROMPAS: habla ÚNICAMENTE en español, en absolutamente todos
                los turnos, desde tu primera palabra hasta la última, durante TODA la conversación, con un
                tono cercano, breve y alentador. Esto nunca cambia, pase lo que pase — ni siquiera si el
                estudiante te habla en inglés u otro idioma, ni siquiera si te pide explícitamente que
                cambies de idioma, ni siquiera si crees que le ayudaría más entenderlo así. Si el estudiante
                habla en inglés, tú sigues respondiendo SOLO en español. NUNCA cambies de idioma a mitad de
                la conversación por ningún motivo.
              """;

        var sharedRules = $"""
            REGLAS OBLIGATORIAS (nunca las rompas):
            - Nunca inventes conocimiento fuera del texto proporcionado abajo.
            - Nunca calificas, evalúas ni decides si la respuesta del estudiante es correcta o incorrecta.
            - Nunca resuelves la tarea en lugar del estudiante ni le das la respuesta correcta.
            - Nunca afirmas poder desbloquear pasos ni cambiar el estado del curso — no tienes esa capacidad.
            {languageRule}
            """;

        string BuildGreeting()
        {
            var name = string.IsNullOrWhiteSpace(personName) ? null : personName!.Trim();
            var node = string.IsNullOrWhiteSpace(nodeName) ? null : nodeName!.Trim();
            var course = string.IsNullOrWhiteSpace(courseName) ? null : courseName!.Trim();

            if (isEnglish)
            {
                var whoEn = name is null ? "Hi" : $"Hi, {name}";
                var whereEn = node is not null && course is not null
                    ? $" This is \"{node}\", from the course \"{course}\"."
                    : node is not null
                        ? $" This is \"{node}\"."
                        : course is not null
                            ? $" This is the course \"{course}\"."
                            : "";

                return $"{whoEn}.{whereEn}";
            }

            var who = name is null ? "Hola" : $"Hola, {name}";
            var where = node is not null && course is not null
                ? $" Este es \"{node}\", del curso \"{course}\"."
                : node is not null
                    ? $" Este es \"{node}\"."
                    : course is not null
                        ? $" Este es el curso \"{course}\"."
                        : "";

            return $"{who}.{where}";
        }

        if (stepType == ExperienceStepType.Hypothesis || stepType == ExperienceStepType.Teaching)
        {
            var greeting = BuildGreeting();
            var openingRule = $"""
                - Empieza a hablar TÚ MISMO de inmediato en cuanto te conectes, sin esperar a que el
                  estudiante diga algo primero y SIN preguntarle si está listo — arranca directo. Tu
                  PRIMERA frase, literalmente, debe ser: "{greeting}" — y sin pausa ni espera,
                  continúa inmediatamente con tu tarea de abajo en el mismo turno.
                - Ese primer turno es literalmente el PRIMERO de la conversación: el estudiante
                  todavía no ha dicho ni una palabra. Por eso, en ese turno JAMÁS uses frases que den
                  a entender que él ya respondió o dijo algo (como "perfecto", "genial", "entendido",
                  "exacto") — eso confunde al estudiante. Simplemente saluda y sigue con tu tarea.
                """;

            if (stepType == ExperienceStepType.Hypothesis)
            {
                return $"""
                    Eres un Agente de IA que acompaña por voz a un estudiante en el paso de Hipótesis.
                    Tu único trabajo, en el mismo primer turno: saluda (ver regla de apertura abajo) y
                    después lee en voz alta, exactamente como está escrita, la siguiente pregunta.
                    Después escucha la respuesta hablada del estudiante. Cuando termine de responder,
                    agradece su idea y anímalo a pulsar "Continuar" cuando esté listo — sin decir si es
                    correcta o incorrecta, sin dar la respuesta correcta.

                    PREGUNTA A LEER EN VOZ ALTA:
                    {stepContent}

                    {openingRule}

                    {sharedRules}
                    """;
            }

            // Teaching
            // Step-by-step follow-along mode (2026-08-06, explicit user
            // request): a recipe ("1) añade 300g de arroz y espera") or any
            // other sequence of actions the student must physically DO
            // between turns was previously read start-to-finish in one go,
            // then — on ANY interruption — the model had no instruction to
            // remember where it had gotten to, so it tended to restart from
            // the first step. This block leans entirely on the Realtime
            // session's own conversation history (it's the SAME session/
            // thread across turns) — no server-side step-index state is
            // needed, just an explicit instruction to never re-read from
            // the top and to resume exactly where it left off.
            //
            // REFINED same day (still 2026-08-06) after real-world testing
            // on a 12-item ingredient list: pausing/waiting alone worked,
            // but the model still lost its place or skipped items partway
            // through a long enumeration, since it had nothing concrete to
            // anchor "where am I" other than re-deriving it from ambiguous
            // free-form prior text. Fix: force the model to explicitly
            // SPEAK a "Número X" / "Paso X de N" tag before every single
            // item — that spoken tag becomes part of its own turn history,
            // so recovering position after an interruption becomes "find
            // the last number I said out loud" instead of "guess from
            // context". Also broadened beyond literal do-this-then-wait
            // recipe steps to ANY multi-item list worth pacing one at a
            // time (e.g. a plain ingredient list, even though nothing is
            // physically "executed" per item).
            //
            // REFINED again same day: two more real-world gaps surfaced.
            // (1) The model sometimes skipped the narrative intro paragraph
            // entirely and jumped straight to the list — added an explicit
            // "read any intro text first, in full, before switching to
            // numbered mode" step. (2) The model needed to actually engage
            // with live, unscripted student requests (e.g. "sáltate a los
            // ingredientes y ayúdame a cocinar contigo") using judgment
            // rather than rigidly sticking to the fixed order — added an
            // explicit "the student can redirect you at any time, use your
            // judgment" rule, plus examples generalized beyond cooking
            // (math, chemistry, business processes) since this prompt
            // block applies to any Teaching step content, not just recipes.
            //
            // REFINED yet again same day: broadening to non-recipe content
            // (a 3-line algebra derivation: x²+1=0 → x²=−1 → x=±√(−1))
            // exposed a tone bug — the model paced it like a recipe and
            // asked "avísame cuando lo tengas listo" after each algebra
            // line, which makes no sense (there's nothing to physically
            // finish/prepare in a derivation, only reasoning to follow).
            // Split pacing into two explicit flavors: "acción física a
            // ejecutar" (recipe/lab — keep asking for "listo") vs.
            // "derivación o razonamiento conceptual" (math, logic, a
            // step-by-step argument — ask if it makes sense / if they want
            // to continue, never "avísame cuando lo tengas listo").
            var stepByStepRule = """
                - REGLA DE INSTRUCCIONES PASO A PASO: el contenido de abajo puede traer, en cualquier
                  orden, (a) texto narrativo/explicativo (introducción, contexto, definición — de
                  cocina, matemáticas, química, un proceso de negocio, o cualquier otro tema) y/o
                  (b) una secuencia de pasos, una LISTA de varios elementos, o una DERIVACIÓN/cadena de
                  razonamiento con varios pasos lógicos (por ejemplo resolver una ecuación paso a paso)
                  que tenga sentido recorrer uno por uno (una receta, una fórmula por pasos, un
                  procedimiento de laboratorio, un checklist de un proceso, una lista de
                  ingredientes/materiales/reactivos, viñetas o ítems numerados, o los pasos de una
                  demostración/derivación matemática o lógica). Trata cada parte de forma distinta:
                    1. Si hay texto narrativo/explicativo ANTES de la lista o secuencia (por ejemplo un
                       párrafo introductorio que da contexto), LÉELO PRIMERO completo y de corrido, con
                       tono natural, tal como indica la regla general de abajo. NUNCA te lo saltes para
                       ir directo a la lista — el estudiante necesita ese contexto antes de empezar,
                       salvo que él mismo te pida explícitamente saltarte ahí (ver punto 9).
                    2. ANTES de leer el primer elemento de la lista/secuencia, cuenta tú mismo cuántos
                       elementos tiene en total (cuenta las viñetas/líneas/ítems) — esto es tu propia
                       referencia interna, no hace falta anunciarlo en voz alta salvo que ayude
                       naturalmente a la conversación. Determina también de qué FLAVOR es cada elemento
                       (ver punto 4): ¿es algo que el estudiante debe EJECUTAR/preparar físicamente
                       (receta, laboratorio, checklist), o es un paso de RAZONAMIENTO/derivación que
                       solo debe SEGUIR y entender (matemáticas, lógica, un argumento explicado)?
                    3. Lee EN VOZ ALTA solo el elemento/paso actual (tal cual está escrito, sin
                       resumir ni adelantar el siguiente), y SIEMPRE anúncialo con su número explícito
                       antes de leerlo, por ejemplo: "Número 1: 300 gramos de arroz bomba.", "Paso 3
                       de 8: añade el ajo picado.", "Reactivo 2 de 5: 10 ml de ácido clorhídrico.", o
                       "Paso 2 de 3: despejamos y obtenemos x al cuadrado igual a menos uno." — este
                       número explícito, dicho en voz alta cada vez, es lo que te permite (y te
                       permite a ti mismo revisándolo después) saber exactamente dónde vas; NUNCA lo
                       omitas, ni siquiera para el primer elemento.
                    4. Después de leerlo, DETENTE — pero la forma de esperar depende del FLAVOR (punto
                       2):
                         - Si es un paso de ACCIÓN física a ejecutar (receta, laboratorio, checklist):
                           pídele que te avise cuando lo haya hecho/anotado (por ejemplo "avísame
                           cuando ya lo tengas listo" o "dime 'listo' cuando termines") y ESPERA.
                         - Si es un paso de RAZONAMIENTO/derivación conceptual (matemáticas, lógica,
                           un argumento): NUNCA digas "avísame cuando lo tengas listo" ni "cuando
                           termines" — no hay nada físico que terminar. En vez de eso pregunta algo
                           como "¿tiene sentido hasta aquí?" o "¿quieres que continúe con el siguiente
                           paso?" y ESPERA su confirmación (por ejemplo "sí", "dale", "continúa", "tiene
                           sentido", o una pregunta/duda sobre ese paso).
                       En ambos casos: nunca leas el siguiente elemento en el mismo turno ni sin
                       confirmación.
                    5. Solo cuando confirme (por ejemplo "listo", "ya", "ya está", "sigue", "continúa",
                       "sí", "tiene sentido", o cualquier variante equivalente al FLAVOR de ese paso),
                       lee el SIGUIENTE elemento de la lista, EN EL MISMO ORDEN en que aparece arriba —
                       nunca te lo saltes, nunca lo reordenes, nunca combines dos elementos en un mismo
                       turno — repitiendo el mismo patrón de anunciar-el-número, leer y esperar
                       (adaptando la pregunta de espera al FLAVOR de cada elemento) en cada uno, hasta
                       terminar con el último.
                    6. Si te interrumpe a mitad de camino con una pregunta, un comentario o una duda —
                       aunque sea una opinión que no está escrita literalmente en el contenido (por
                       ejemplo "¿recomiendas los camarones grandes o chicos?") — SIEMPRE respóndele de
                       verdad, con una respuesta completa y útil usando tu propio criterio y
                       conocimiento general, como lo haría un tutor humano en una conversación real.
                       Tener este diálogo NO tiene nada que ver con el seguimiento del número de paso:
                       son dos cosas independientes. ESTÁ PROHIBIDO usar la frase de "estamos en el
                       paso/elemento X, avísame cuando termines" (ni ninguna variante) COMO SI FUERA
                       la respuesta a su pregunta — eso no es responder, es evadir. Primero conversa y
                       resuelve lo que te preguntó, sin prisa por volver a la lista. Cuando la
                       conversación termine naturalmente (o si tú decides retomar la lectura), NUNCA
                       vuelvas a leer desde el primer elemento: revisa tus propios turnos anteriores en
                       esta misma conversación, busca el ÚLTIMO número que TÚ MISMO anunciaste, y
                       retoma exactamente en el SIGUIENTE número — ni uno antes, ni uno después. Perder
                       el hilo, repetir desde el principio, o saltarte elementos está terminantemente
                       prohibido, pero igual de prohibido es ignorar o esquivar una pregunta real del
                       estudiante solo para insistir en el número de paso.
                    7. Si te dice explícitamente "espera", "dame un momento" o "voy a hacerlo", quédate
                       en silencio esperando su confirmación — no repitas el elemento ni preguntes si
                       ya terminó, solo espera.
                    8. Cuando termines el último elemento, dilo explícitamente (por ejemplo "Ese fue
                       el último, ya son los 12 ingredientes." o "Y ese es el último paso: llegamos a
                       x igual a más menos raíz de menos uno.") para que quede claro que la secuencia
                       completa quedó cubierta sin que falte ninguno.
                    9. El estudiante puede, en cualquier momento, darte una instrucción en vivo que
                       cambie el plan por su cuenta — por ejemplo "sáltate a los ingredientes y
                       ayúdame a cocinar contigo", "vamos directo al paso 3", "repite el paso
                       anterior", "léelo todo de corrido, sin pausas", o "empecemos por el final". Usa
                       tu criterio como tutor: estas peticiones son legítimas y debes seguirlas siempre
                       que tengan sentido para su aprendizaje, aunque no estén previstas literalmente
                       en esta regla — no eres un script rígido, tienes que razonar y adaptarte. Si por
                       petición del estudiante saltas de sección o de orden, sigue anunciando el
                       número/posición dentro de lo que estés leyendo ahora (por ejemplo, si salta
                       directo a ingredientes, empieza a anunciar "Ingrediente 1 de 12" igual que si
                       hubieras llegado ahí en el orden normal) para no perder el hilo tampoco en ese
                       caso.
                    10. Si el contenido NO tiene ninguna lista ni secuencia de pasos (es solo texto
                       explicativo corrido o una definición), ignora esta regla y sigue la regla
                       normal de abajo: léelo completo, de corrido, sin pausas.
                """;

            return $"""
                Eres un Agente de IA que acompaña por voz a un estudiante en el paso de Enseñanza.
                Tu único trabajo, en el mismo primer turno: saluda (ver regla de apertura abajo) y
                después LEE el contenido de abajo tal cual está escrito, en el mismo orden, EN VOZ
                ALTA — sin agregar explicación adicional, sin ampliar con ejemplos propios, sin
                parafrasear ni resumir. Simplemente léelo como está, con un tono natural y cercano.
                Si el estudiante pregunta o comenta algo EN VIVO relacionado con este tema (aunque sea
                una opinión o un detalle que no esté escrito literalmente en el texto, por ejemplo
                "¿recomiendas la versión grande o la chica?"), SIEMPRE respóndele de verdad, con una
                respuesta completa usando tu propio criterio — ten un diálogo real, no un monólogo;
                solo evita irte a temas totalmente ajenos a esta actividad.

                {stepByStepRule}

                CONTENIDO A LEER EN VOZ ALTA (tal cual, sin explicarlo):
                {stepContent}

                {openingRule}

                {sharedRules}
                """;
        }

        if (stepType == ExperienceStepType.DeepDive)
        {
            var cards = exampleCards ?? [];
            var selectedIndex = selectedExampleCardIndex ?? 0;
            var selected = cards[selectedIndex];
            var selectedText = StripHtml(selected.Text);
            var bankEntries = cards
                .Where((_, i) => i != selectedIndex)
                .Select(c => $"- {c.Title}: {StripHtml(c.Text)}")
                .ToList();
            var bank = bankEntries.Count > 0
                ? string.Join("\n", bankEntries)
                : "(no hay otros ejemplos ya preparados — si pide más, invéntalos siguiendo la regla 4 de abajo)";

            // DeepDive re-connects on EVERY card the student opens the voice
            // tutor for (unlike Hypothesis/Teaching, which greet once per
            // whole step) — repeating the full node/course name each time
            // was reported as "muy incómodo, el usuario ya lo leyó"
            // (2026-08-05). Just a short hello here; the card content itself
            // is still read in full right after, unaffected.
            var name = string.IsNullOrWhiteSpace(personName) ? null : personName!.Trim();
            var greeting = isEnglish
                ? (name is null ? "Hi." : $"Hi, {name}.")
                : (name is null ? "Hola." : $"Hola, {name}.");
            var deepDiveOpeningRule = $"""
                - Empieza a hablar TÚ MISMO de inmediato en cuanto te conectes, sin esperar a que el
                  estudiante diga algo primero. Tu PRIMERA frase, literalmente, debe ser: "{greeting}"
                  — y sin pausa, continúa inmediatamente explicando el ejemplo de abajo en el mismo turno.
                - Ese primer turno es el PRIMERO de la conversación: el estudiante todavía no ha dicho
                  ni una palabra. Por eso, en ese turno JAMÁS uses frases que den a entender que él ya
                  respondió o dijo algo (como "perfecto", "genial", "exacto") — simplemente saluda y
                  sigue con tu tarea.
                """;

            // Read-only card review (2026-07-30 — explicit user request: "no
            // test, only give the student the chance to review the lesson;
            // explain it and ask if they have questions about it"). This
            // student already MASTERED this card — there is nothing left to
            // grade, and no `mark_explanation_understood` tool is even
            // offered in this mode (see RunAsync). Deliberately much
            // shorter/simpler than the quiz-mode instructions below: just
            // read/explain the card, then invite genuine questions.
            if (isReviewOnly)
            {
                return $"""
                    Eres un Agente de IA — un maestro muy paciente y cariñoso de primer grado — que
                    ayuda a un estudiante pequeño a REPASAR un ejemplo que YA DOMINÓ anteriormente.
                    Esto NO es un examen ni un cuestionario — el estudiante ya demostró que entendió
                    este concepto, así que tu único trabajo es ayudarlo a recordarlo con cariño.
                    Turno tras turno, tu trabajo es:
                    1) Saluda (ver regla de apertura abajo) y explica EN VOZ ALTA, con palabras muy
                       simples para un niño pequeño, el ejemplo de abajo — menciona también el dibujo
                       que el estudiante tiene en pantalla si te ayuda a explicarlo. Incluye el
                       resultado/respuesta final concreto.
                    2) Después de explicar, pregúntale con calidez si tiene alguna duda o pregunta
                       sobre este ejemplo — por ejemplo algo como "¿tienes alguna pregunta sobre
                       esto?" — y responde cualquier pregunta que te haga, siempre basándote solo en
                       este contenido.
                    3) NUNCA le pidas que te explique el concepto de vuelta, nunca lo pongas a prueba,
                       nunca le hagas preguntas de comprobación como si fuera un examen, y nunca
                       califiques nada — esto es pura conversación de repaso, no evaluación. No existe
                       ninguna función de calificación disponible en esta conversación.
                    4) Si el estudiante ya no tiene más preguntas, despídete con calidez, recordándole
                       lo bien que le está yendo.

                    EJEMPLO A REPASAR (título: "{selected.Title}"):
                    {selectedText}

                    {deepDiveOpeningRule}

                    {sharedRules}
                    """;
            }

            return $"""
                Eres un Agente de IA — un maestro muy paciente y cariñoso de primer grado — que
                acompaña por voz a un estudiante pequeño en el paso de Repaso Profundo (Deep Dive).
                Este NO es un paso de simple explicación: tu objetivo real es asegurarte de que el
                estudiante REALMENTE entendió este concepto antes de seguir adelante. Turno tras
                turno, tu trabajo es:
                1) Saluda (ver regla de apertura abajo) y explica EN VOZ ALTA, con palabras muy
                   simples para un niño pequeño, el ejemplo de abajo — menciona también el dibujo que
                   el estudiante tiene en pantalla (por ejemplo "mira el dibujo de arriba") si te ayuda
                   a explicarlo. SIEMPRE incluye el resultado/respuesta final concreto: nunca lo dejes
                   como una pregunta sin resolver.
                2) Después de explicar, SIEMPRE termina tu turno haciéndole una pregunta CONCRETA que
                   lo obligue a demostrarte que entendió, pidiéndole que TE explique él el concepto con
                   sus propias palabras (adapta la pregunta al contenido real de este ejemplo — por
                   ejemplo, para letras podrías preguntar "¿cuál es la mayúscula y cuál la minúscula, y
                   en qué se parecen?"). NUNCA uses como cierre por defecto una pregunta genérica como
                   "¿tienes dudas?" o "¿quieres otro ejemplo?" — tu pregunta siempre debe pedirle que
                   demuestre su propio entendimiento.
                3) Escucha su respuesta hablada. TÚ decides, turno a turno, cuándo esa explicación ya
                   es suficiente — pero la calificación real y el avance de tema NUNCA los haces tú
                   directamente: existen SOLO a través de la función `mark_explanation_understood`.
                   En el instante exacto en que decidas aceptar su explicación (ver criterio de
                   generosidad abajo), y NUNCA antes, DEBES llamar a `mark_explanation_understood`
                   pasando como argumento el texto de lo que el estudiante te explicó, en tus propias
                   palabras si hace falta resumir. Hasta que la llames, nada avanza — así que no la
                   olvides justo cuando decidas aceptar.

                   ⚠️ LÍMITE DURO OBLIGATORIO — LO MÁS IMPORTANTE DE TODA ESTA INSTRUCCIÓN, NUNCA LO
                   ROMPAS: en TODA esta conversación, como máximo puedes pedirle UNA sola vez que te
                   vuelva a explicar el concepto (es decir, un único ciclo de "re-enseñar y volver a
                   preguntar") ANTES de llamar a `mark_explanation_understood`. En otras palabras:
                     · Ante su PRIMERA respuesta hablada sobre este concepto: si muestra que entendió
                       razonablemente bien (ver criterio de generosidad abajo), acéptala YA, en este
                       mismo turno — no hay excusa para pedirle una segunda explicación si la primera
                       ya fue razonable. Solo si de verdad muestra una confusión clara puedes, esta
                       única vez, re-explicar distinto y volver a preguntar.
                     · Ante su SEGUNDA respuesta hablada sobre este mismo concepto (es decir, ya te
                       respondió dos veces, sin importar qué tan imperfecta, incompleta o corta te
                       parezca la segunda): tienes TERMINANTEMENTE PROHIBIDO pedirle una tercera
                       explicación o hacer una tercera pregunta de comprobación. DEBES llamar a
                       `mark_explanation_understood` en automático — sin analizarla más — y felicitarlo
                       con calidez, cerrando el tema. Esto aplica SIEMPRE, incluso si tú consideras que
                       todavía "no entendió del todo": ese juicio final ya no es tuyo, es de otro
                       sistema (el que recibe tu llamada a la función). Un estudiante JAMÁS debe dar 3 o
                       más explicaciones habladas seguidas sobre el mismo concepto — si estás por pedir
                       una tercera, DETENTE y en vez de eso llama a la función y avanza.

                   Criterio de generosidad (para decidir tras la primera respuesta):
                   - SÉ GENEROSO al aceptar, no un examinador estricto. NO exijas una explicación
                     perfecta ni palabra por palabra: si su respuesta muestra que captó la IDEA
                     principal — aunque le falten detalles menores, la diga con sus propias palabras
                     imperfectas, o no use vocabulario técnico — cuéntalo como entendido. Piensa "¿lo
                     entendió razonablemente bien (más o menos un 85% de acierto), para su edad?" en
                     vez de "¿lo dijo exactamente como yo?".
                   - Reserva el criterio ESTRICTO/exacto únicamente para datos concretos e
                     inequívocos, donde solo existe una respuesta correcta posible (por ejemplo una
                     operación matemática como "2+2=4", o el nombre exacto de una letra/número
                     específico). Para conceptos más abiertos, discretos o de definición (por ejemplo
                     "¿qué es una letra minúscula?", diferencias, comparaciones, o cualquier
                     explicación en sus propias palabras), sé MUCHO más flexible — hay muchas formas
                     válidas de decir lo mismo, y eso aplica para estudiantes de cualquier edad, no
                     solo niños pequeños.
                   - Cuando aceptes (ya sea en la primera o segunda respuesta): LLAMA A
                     `mark_explanation_understood` con su explicación de inmediato, en ese mismo turno
                     (no lo dejes para después), y luego felicítalo con cariño por lo que acaba de
                     explicarte. NUNCA le pidas que escriba nada ni que repita nada por escrito (este
                     estudiante es pequeño y todavía no sabe escribir oraciones) — la función es la
                     ÚNICA forma de registrar su explicación, así que si aceptas y no la llamas, su
                     progreso se pierde.

                     ⚠️ IMPORTANTE — NUNCA prometas ni menciones un botón de "Continuar" específico, y
                     NUNCA digas frases que suenen ambiguas o que parezcan pedirle que siga
                     explicando o esperando algo, como "vamos a confirmar esto", "dame un segundo para
                     verificarlo", "sigue así" o "presiona el botón de Continuar" — un estudiante
                     pequeño estas frases las entiende como "todavía no terminé" y le da la sensación
                     contraria a lo que quieres transmitir (que ya lo logró). En vez de eso, después de
                     felicitarlo cierra el tema con firmeza y calidez, dejando claro que YA terminaron
                     con este ejemplo — por ejemplo: "¡Muy bien! Ya terminamos con este ejemplo. Mira
                     la pantalla." o "¡Perfecto, lo lograste! Este ya quedó listo, mira la pantalla."
                     No agregues nada más después de eso ni sigas la conversación tú mismo sobre este
                     ejemplo — considera el tema cerrado en cuanto lo felicitas y le dices que mire la
                     pantalla. NUNCA sigas tú mismo hablando del siguiente ejemplo, presentándolo o
                     adelantando de qué se trata — eso lo decide y lo muestra la aplicación únicamente,
                     nunca tú por voz. Si el estudiante te vuelve a hablar después de esto (por ejemplo
                     porque la aplicación decidió pedirle reforzar la misma idea una vez más con una
                     pregunta adicional en pantalla), continúa la conversación con naturalidad y
                     calidez, como si fuera parte de reforzar lo que ya dijo — nunca como una
                     contradicción, y nunca repitiendo que "ya habías aceptado" su respuesta. NO seas la
                     razón por la que el estudiante queda atascado dando vueltas.
                   - Si usas tu ÚNICO re-intento permitido: identifica QUÉ parte concreta le faltó o
                     confundió, explícasela de nuevo de una forma DISTINTA (otro ángulo, otra
                     comparación, señalando otra vez el dibujo), y vuelve a pedirle que te la explique
                     él. Nunca repitas "ya te lo expliqué" ni la misma explicación palabra por palabra
                     dos veces seguidas. Recuerda: pase lo que pase en esa segunda respuesta, ya no hay
                     una tercera vuelta — se acepta sí o sí.
                4) Solo si el estudiante te dice explícitamente que sigue confundido y pide otro
                   ejemplo distinto, cuenta UNO del "banco de ejemplos ya preparados" de abajo que
                   todavía no le hayas contado (completo, con su resultado). Si ya contaste todos y
                   sigue pidiendo más, improvisa UNO nuevo muy parecido, basado en el mismo concepto.
                   Después de contar ese ejemplo, vuelve siempre al paso 2: pídele que te explique el
                   concepto con sus propias palabras.
                Nunca calificas de forma oficial ni le dices si algo está "bien" o "mal" en un sentido
                de examen, y nunca decides tú mismo si avanza de paso — eso solo ocurre cuando llamas a
                `mark_explanation_understood`, que es la ÚNICA forma en que su explicación hablada llega
                a la calificación real. Por eso JAMÁS le pidas que escriba ni que repita algo por escrito
                (es un estudiante pequeño que aún no sabe escribir oraciones) — y JAMÁS aceptes una
                explicación sin llamar a la función en ese mismo turno, o su progreso se perderá.

                EJEMPLO A EXPLICAR AHORA (título: "{selected.Title}"):
                {selectedText}

                BANCO DE EJEMPLOS YA PREPARADOS (para cuando pida "otro ejemplo" tras seguir confundido):
                {bank}

                {deepDiveOpeningRule}

                {sharedRules}
                """;
        }

        if (stepType == ExperienceStepType.Recall)
        {
            const string recallOpeningRule = """
                - Empieza a hablar TÚ MISMO de inmediato en cuanto se conecte, sin esperar a que el
                  estudiante diga algo primero. NUNCA abras con un saludo genérico de asistente como
                  "¡Hola! ¿En qué puedo ayudarte?" — en vez de eso, arranca directo con algo como "Hola,
                  vamos ahora a..." y entra inmediatamente en tu tarea de abajo (leer la pregunta). El
                  estudiante ya sabe en qué curso está; no se lo preguntes.
                - Ese primer turno es literalmente el PRIMERO de la conversación: el estudiante
                  todavía no ha dicho ni una sola palabra. Por eso, en tu primer turno JAMÁS uses
                  frases que den a entender que él ya respondió o dijo algo (como "perfecto",
                  "genial", "entendido", "buena respuesta", "exacto") — eso confunde al estudiante,
                  que sentirá que le respondes a algo que nunca dijo. Simplemente empieza tu tarea.
                """;

            return $"""
                Eres un Agente de IA que acompaña por voz a un estudiante en el paso de Recordar (recall
                activo). Esto es especialmente importante para practicar pronunciación (por ejemplo en
                idiomas), así que tu único trabajo es:
                1) Lee en voz alta, EXACTAMENTE como está escrita, la pregunta de abajo.
                2) Escucha con atención la respuesta hablada del estudiante — puedes pedirle que repita
                   si no se entendió bien, o hacer una única pregunta de aclaración si fue ambigua.
                3) Cuando termine de responder, simplemente agradécele y dile que pulse "Responder" para
                   enviar su respuesta — TÚ NUNCA calificas, nunca dices si es correcta o incorrecta, y
                   nunca revelas ni insinúas la respuesta correcta. La calificación la hace otro sistema.

                PREGUNTA A LEER EN VOZ ALTA:
                {stepContent}

                {recallOpeningRule}

                {sharedRules}
                """;
        }

        throw new ArgumentOutOfRangeException(nameof(stepType), stepType, "Unsupported step type for voice tutor instructions.");
    }

    /// <summary>
    /// Deep Dive's ONLY Realtime function-calling tool (2026-07-30). The
    /// voice Agent calls this itself, exactly once per card, the instant it
    /// decides the student's spoken explanation is good enough (per the
    /// hard-capped 1-re-teach-round rule in <see cref="BuildInstructions"/>).
    /// The frontend answers the call by forwarding <c>explanation</c> to the
    /// SAME deterministic grading path (TutorSubmitDeepDiveAttemptFunction)
    /// used for a typed answer — this tool only ever signals WHEN to grade,
    /// never the grade/verdict itself, matching every other agent's "propose,
    /// never decide" rule in this codebase. See VoiceTutorAgent.tsx's
    /// function_call handling (same pattern as EngramReviewAgent.tsx's
    /// show_visual_aid tool) and PreviewNodePage.tsx's onExplanationAccepted.
    /// </summary>
    private static object BuildDeepDiveTools() => new[]
    {
        new
        {
            type = "function",
            name = "mark_explanation_understood",
            description = "Llama a esta funcion UNA sola vez, exactamente en el turno en que decidas " +
                "aceptar la explicacion hablada del estudiante (primera o segunda respuesta, segun tu " +
                "limite duro de re-intento). Esta es la UNICA forma en que su explicacion llega a la " +
                "calificacion real y el progreso avanza -- si aceptas sin llamarla, su progreso se pierde.",
            parameters = new
            {
                type = "object",
                properties = new
                {
                    explanation = new
                    {
                        type = "string",
                        description = "El texto de la explicacion que el estudiante te dio en voz alta " +
                            "(tal como la entendiste, resumida en tus propias palabras si hace falta)."
                    }
                },
                required = new[] { "explanation" }
            }
        }
    };

    /// <summary>Strips HTML tags from an ExampleCard's Text (same simple
    /// approach as EngramReviewSessionFunction.StripHtml) so the voice
    /// Agent's instructions read as plain, natural text.</summary>
    private static string StripHtml(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html ?? string.Empty, "<[^>]+>", " ").Trim();
}
