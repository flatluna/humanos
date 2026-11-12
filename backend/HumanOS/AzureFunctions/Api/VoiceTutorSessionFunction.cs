using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
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
    private readonly ILearnerContextBuilder _learnerContextBuilder;
    private readonly PersonService _personService;
    private readonly StructuredLearningRecallEngine _recallEngine;

    public VoiceTutorSessionFunction(
        RealtimeVoiceSessionService voiceSessionService,
        HumanOsDbContext dbContext,
        ILearnerContextBuilder learnerContextBuilder,
        PersonService personService,
        StructuredLearningRecallEngine recallEngine)
    {
        _voiceSessionService = voiceSessionService;
        _dbContext = dbContext;
        _learnerContextBuilder = learnerContextBuilder;
        _personService = personService;
        _recallEngine = recallEngine;
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

        // AI Agents course chat "Talk about this" voice path (2026-09-04) —
        // checked first since it takes priority over every other target field.
        if (body.AiAgentQuestion is not null)
        {
            return await RunAiAgentQuestionAsync(request, body.AiAgentQuestion, body.PersonName, body.PersonId, cancellationToken);
        }

        // Traditional Course path (2026-08-09) — a single CapabilityLesson
        // (subchapter), no graph/blueprint/session at all. Scoped to ONE
        // lesson, not its whole parent chapter, to match Graph mode's
        // per-node granularity. Handled entirely separately from the
        // ExperienceStepType-oriented flow below.
        if (body.TraditionalCourseLessonId.HasValue)
        {
            return await RunTraditionalLessonAsync(request, body.TraditionalCourseLessonId.Value, body.PersonName, body.PersonId, cancellationToken);
        }

        if (body.StructuredLearningSectionStepId.HasValue)
        {
            return await RunStructuredLearningStepAsync(
                request, body.StructuredLearningSectionStepId.Value, body.PersonName, body.ExampleCardIndex, body.IsReviewOnly, body.IsQaMode, body.PersonId, body.CurrentPromptText, body.RecallCorrectAnswer, cancellationToken);
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
            select new { CapabilityId = c.CapabilityId, NodeName = n.Name, CourseName = c.Name, c.ContentLanguageCode }
        ).FirstOrDefaultAsync(cancellationToken);

        // Read-only card review (2026-07-30): only meaningful for a DeepDive
        // blueprint-only session (revisiting an ALREADY-MASTERED card via
        // Capability Studio's card index) — never for the live-session path,
        // which always needs the real grading loop.
        var isReviewOnly = body.IsReviewOnly && stepType == ExperienceStepType.DeepDive;

        var instructions = BuildInstructions(
            stepType, effectiveContent, body.PersonName, courseContext?.NodeName, courseContext?.CourseName,
            courseContext?.ContentLanguageCode, exampleCards, body.ExampleCardIndex, isReviewOnly);

        // Personalized LEARNER CONTEXT injection (at the start of the system prompt)
        var employeeId = body.PersonId ?? await ResolveAuthenticatedEmployeeIdAsync(request, cancellationToken);
        var learnerContext = await _learnerContextBuilder.BuildLearnerContextBlockAsync(
            employeeId, courseContext?.CapabilityId, capabilityGraphNodeId.ToString(), cancellationToken);

        if (!string.IsNullOrWhiteSpace(learnerContext))
        {
            var deepDiveOverride = stepType == ExperienceStepType.DeepDive
                ? "\n\nCRITICAL OVERRIDE FOR DEEP DIVE:\nIgnore any instruction above to give a multi-sentence opening greeting about career objectives. You must strictly follow the DEEP DIVE OPENING RULE: greet briefly, then give ONE genuine, SPECIFIC sentence connecting THIS card's actual concept to the learner's real role/objectives/tools (using real details from LEARNER CONTEXT, worded differently than on other cards — never a repeated generic template), then immediately present the exact card text. You MUST still fully and faithfully cover the ENTIRE card content below, exactly as the DEEP DIVE rules require — the personalization sentence is only a brief hook, never a substitute for actually explaining the card.\n\n"
                : "\n\n";
            instructions = $"{learnerContext}{deepDiveOverride}{instructions}";
        }

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
    /// <summary>
    /// AI Agents course chat's "Talk about this" voice path (2026-09-04) —
    /// grounded in the SAME question/answer/engrams already produced by the
    /// text chat (StructuredLearningKnowledgeRetrievalService's reranked
    /// top-3, passed straight through by the frontend from the chat
    /// response it already has), plus the same personalized LEARNER
    /// CONTEXT used everywhere else. No DB re-fetch of engram content —
    /// the excerpts are exactly what the student already saw as citation
    /// chips, so voice can never disagree with the text answer on screen.
    /// </summary>
    private async Task<HttpResponseData> RunAiAgentQuestionAsync(
        HttpRequestData request, AiAgentQuestionContext context, string? personName, Guid? explicitPersonId, CancellationToken cancellationToken)
    {
        var employeeId = explicitPersonId ?? await ResolveAuthenticatedEmployeeIdAsync(request, cancellationToken);
        var learnerContext = await _learnerContextBuilder.BuildLearnerContextBlockAsync(
            employeeId, context.DocumentId, engramId: null, cancellationToken);

        // 2026-09-04 fix (2nd pass): ground language in the actual ENGRAM
        // EXCERPTS (real course content — see context.Engrams below), never
        // in the document's own stored config or the text answer. Two
        // separate signals turned out unreliable: (1) StructuredLearningTutorAgent
        // replies in whatever language the STUDENT typed, not the course's own
        // language, so an English course answered via a Spanish-typed question
        // came back Spanish; (2) this specific document's own ConfigSnapshotJson
        // languageCode was found to be "es" even though its real generated
        // content is 100% English — a data mismatch, not something code should
        // trust blindly. The engram excerpts are the one signal that can never
        // lie about what language the course material is actually written in.
        var documentLanguageCode = context.Engrams.Count > 0
            ? DetectLanguageCode(string.Join(" ", context.Engrams.Select(e => e.Text)))
            : null;
        if (documentLanguageCode is null && context.DocumentId is Guid documentId)
        {
            documentLanguageCode = ResolveDocumentLanguageCode(
                await _dbContext.StructuredLearningDocuments
                    .AsNoTracking()
                    .Where(d => d.StructuredLearningDocumentId == documentId)
                    .Select(d => d.ConfigSnapshotJson)
                    .FirstOrDefaultAsync(cancellationToken));
        }

        var instructions = BuildAiAgentQuestionInstructions(context, personName, documentLanguageCode);
        if (!string.IsNullOrWhiteSpace(learnerContext))
        {
            instructions = $"{learnerContext}\n\n{instructions}";
        }

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

    /// <summary>Q&amp;A-only prompt (mirrors <see cref="BuildStructuredLearningQaInstructions"/>'s
    /// "greet once, then wait silently" pattern) — grounded strictly in the
    /// already-given Question/Answer plus the cited engrams' own excerpts,
    /// never a full narrated lecture.</summary>
    private static string BuildAiAgentQuestionInstructions(AiAgentQuestionContext context, string? personName, string? documentLanguageCode = null)
    {
        var isEnglish = (documentLanguageCode ?? DetectLanguageCode(context.Answer)) == "en";
        var name = string.IsNullOrWhiteSpace(personName) ? null : personName.Trim();

        var engramsBlock = string.Join(
            "\n\n",
            context.Engrams.Select(e => $"### {e.Title}\n{e.Text}"));

        var greeting = isEnglish
            ? (name is null ? "Hi. Want to talk more about this?" : $"Hi, {name}. Want to talk more about this?")
            : (name is null ? "Hola. ¿Quieres hablar más sobre esto?" : $"Hola, {name}. ¿Quieres hablar más sobre esto?");

        if (isEnglish)
        {
            return $"""
                You are an AI Agent following up, BY VOICE, on a question a student already asked in text
                chat and already got a text answer for. This is a QUESTION-AND-ANSWER conversation, NOT a
                class — you never teach unprompted.

                The student's ORIGINAL QUESTION was: "{context.Question}"
                The TEXT ANSWER already shown to them was: "{context.Answer}"

                Start talking YOURSELF immediately upon connecting. Your first and ONLY unprompted sentence
                must be exactly: "{greeting}" — then STOP TALKING and wait silently for the student's spoken
                follow-up. NEVER re-read the answer aloud unprompted — that is the single most important
                rule here.

                For the rest of the conversation:
                - LANGUAGE — HARD RULE, NEVER BREAK IT: speak ONLY in English, every turn.
                - Answer follow-up questions grounded STRICTLY in the ORIGINAL QUESTION/ANSWER above and the
                  ENGRAM EXCERPTS below — never invent facts, numbers, or examples that aren't there.
                - Keep answers brief and conversational — a direct answer, not a lecture.
                - After answering, STOP and wait silently for the next question.
                - If the student says they have no more questions or wants to end, say a brief goodbye and
                  stop talking.
                - NEVER grade the student or claim to affect their progress.

                ENGRAM EXCERPTS (your only source of truth beyond the answer above):
                {engramsBlock}
                """;
        }

        return $"""
            Eres un Agente de IA dando seguimiento, POR VOZ, a una pregunta que el estudiante ya hizo en el
            chat de texto y que ya tiene una respuesta de texto. Esta es una conversación de PREGUNTAS Y
            RESPUESTAS, NO una clase — nunca enseñas sin que te lo pidan.

            La PREGUNTA ORIGINAL del estudiante fue: "{context.Question}"
            La RESPUESTA DE TEXTO que ya vio fue: "{context.Answer}"

            Empieza a hablar TÚ MISMO de inmediato en cuanto te conectes. Tu primera y ÚNICA frase sin que te
            lo pidan debe ser exactamente: "{greeting}" — y después CÁLLATE y espera en silencio la pregunta
            hablada del estudiante. NUNCA vuelvas a leer la respuesta en voz alta sin que te lo pidan — esa
            es la regla más importante de todo este prompt.

            Para el resto de la conversación:
            - IDIOMA — REGLA DURA, NUNCA LA ROMPAS: habla ÚNICAMENTE en español, en todos los turnos.
            - Responde preguntas de seguimiento basándote ESTRICTAMENTE en la PREGUNTA/RESPUESTA original de
              arriba y en los EXTRACTOS DE ENGRAMAS de abajo — nunca inventes datos, cifras ni ejemplos que
              no estén ahí.
            - Mantén las respuestas breves y conversacionales — una respuesta directa, no una clase.
            - Después de responder, DETENTE y espera en silencio la siguiente pregunta.
            - Si el estudiante dice que no tiene más preguntas o quiere terminar, despídete brevemente y
              deja de hablar.
            - NUNCA calificas al estudiante ni afirmas poder afectar su progreso.

            EXTRACTOS DE ENGRAMAS (tu única fuente de verdad además de la respuesta de arriba):
            {engramsBlock}
            """;
    }

    private async Task<HttpResponseData> RunTraditionalLessonAsync(
        HttpRequestData request, Guid lessonId, string? personName, Guid? explicitPersonId, CancellationToken cancellationToken)
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

        // Personalized LEARNER CONTEXT injection
        var employeeId = explicitPersonId ?? await ResolveAuthenticatedEmployeeIdAsync(request, cancellationToken);
        var learnerContext = await _learnerContextBuilder.BuildLearnerContextBlockAsync(
            employeeId,
            lesson.CapabilityChapter.CapabilityId,
            lesson.CapabilityLessonId.ToString(),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(learnerContext))
        {
            instructions = $"{learnerContext}\n\n{instructions}";
        }

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
        HttpRequestData request, Guid sectionStepId, string? personName, int? exampleCardIndex, bool isReviewOnly, bool isQaMode, Guid? explicitPersonId, string? currentPromptText, string? recallCorrectAnswer, CancellationToken cancellationToken)
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

        // Personalized LEARNER CONTEXT resolution
        var employeeId = explicitPersonId ?? await ResolveAuthenticatedEmployeeIdAsync(request, cancellationToken);
        if (!employeeId.HasValue)
        {
            var defaultProfilePerson = await _dbContext.PersonProfiles.AsNoTracking()
                .Where(pp => (pp.DisplayName != null && pp.DisplayName.Contains("Alejandro")) || (pp.FirstName != null && pp.FirstName.Contains("Alejandro")))
                .Select(pp => (Guid?)pp.PersonId)
                .FirstOrDefaultAsync(cancellationToken);

            employeeId = defaultProfilePerson ?? await _dbContext.People.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.CreatedDate)
                .Select(p => (Guid?)p.PersonId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var courseId = step.Section.Document?.StructuredLearningDocumentId;
        var engramId = step.Section.StructuredLearningDocumentSectionId.ToString();

        if (employeeId.HasValue)
        {
            var pProfile = await _dbContext.PersonProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.PersonId == employeeId.Value, cancellationToken);
            if (pProfile is not null && (!string.IsNullOrWhiteSpace(pProfile.DisplayName) || !string.IsNullOrWhiteSpace(pProfile.FirstName)))
            {
                personName = !string.IsNullOrWhiteSpace(pProfile.FirstName) ? pProfile.FirstName : pProfile.DisplayName;
            }
        }
        else if (!string.IsNullOrWhiteSpace(personName) && personName.Contains("@"))
        {
            var pByEmail = await _dbContext.People.AsNoTracking().FirstOrDefaultAsync(p => p.Email == personName.Trim(), cancellationToken);
            if (pByEmail is not null)
            {
                employeeId = pByEmail.PersonId;
                var pProfile = await _dbContext.PersonProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.PersonId == pByEmail.PersonId, cancellationToken);
                personName = pProfile?.FirstName ?? pProfile?.DisplayName ?? personName.Split('@')[0];
            }
            else
            {
                personName = personName.Split('@')[0];
            }
        }

        var learnerContext = await _learnerContextBuilder.BuildLearnerContextBlockAsync(
            employeeId,
            courseId,
            engramId,
            cancellationToken,
            engramTitle: step.Section.SubchapterTitle);

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

            var detectedLanguageCode = ResolveDocumentLanguageCode(
                step.Section.Document?.ConfigSnapshotJson,
                string.Join(" ", exampleCards.Select(c => c.Text)));
            var effectiveIsReviewOnly = isReviewOnly;
            var deepDiveInstructions = BuildInstructions(
                ExperienceStepType.DeepDive, step.Content, personName, nodeName: null, courseName: null,
                detectedLanguageCode, exampleCards, exampleCardIndex, effectiveIsReviewOnly);

            if (!string.IsNullOrWhiteSpace(learnerContext))
            {
                deepDiveInstructions = $"{learnerContext}\n\nCRITICAL OVERRIDE FOR DEEP DIVE:\nIgnore any instruction above to give a multi-sentence opening greeting about career objectives. You must strictly follow the DEEP DIVE OPENING RULE: greet briefly, then give ONE genuine, SPECIFIC sentence connecting THIS card's actual concept to the learner's real role/objectives/tools (using real details from LEARNER CONTEXT, worded differently than on other cards — never a repeated generic template), then immediately present the exact card text. You MUST still fully and faithfully cover the ENTIRE card content below, exactly as the DEEP DIVE rules require — the personalization sentence is only a brief hook, never a substitute for actually explaining the card.\n\n{deepDiveInstructions}";
            }

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
            && step.StepType != Models.Capabilities.Studio.StructuredLearningStepType.Teaching
            && step.StepType != Models.Capabilities.Studio.StructuredLearningStepType.Recall)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.BadRequest,
                "VoiceNotYetSupportedForStep",
                $"Voice Tutor is only available for Hypothesis, Teaching, Recall and DeepDive steps so far (this step is {step.StepType}).",
                cancellationToken);
        }

        // Structured Learning Recall (2026-10-25, revised to full voice
        // control): the Agent reads the CURRENT question aloud (passed by
        // the frontend as currentPromptText for the FIRST question only —
        // every question after that is generated by the real
        // StructuredLearningRecallEngine and handed back to the Agent
        // through the `submit_recall_answer` tool's response, so the Agent
        // NEVER invents a question itself, it only ever reads one that the
        // system already produced). The Agent itself still never decides
        // correctness — it calls the tool with the student's spoken
        // answer, the REAL deterministic engine
        // (SubmitStructuredLearningRecallAttemptFunction, invoked by the
        // frontend's tool-call handler) grades it and returns the
        // true/false verdict + feedback + the next pregenerated question,
        // which the Agent then simply reads back to the student.
        if (step.StepType == Models.Capabilities.Studio.StructuredLearningStepType.Recall)
        {
            if (string.IsNullOrWhiteSpace(currentPromptText))
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "MissingFields",
                    "CurrentPromptText (the question currently on screen) is required for a Structured Learning Recall voice session.",
                    cancellationToken);
            }

            var recallLanguageCode = ResolveDocumentLanguageCode(step.Section.Document?.ConfigSnapshotJson, currentPromptText);
            var isYoungLearnerRecall = step.Section.Document?.PedagogicalAudience == "YoungLearner";

            // Whole-session voice Recall redesign, re-adopted (2026-10-27,
            // explicit product direction after fixing a real client-side bug:
            // `session.update` was missing the Realtime API's now-required
            // `session.type` field, so EVERY connection's turn_detection/VAD
            // tuning was silently rejected server-side the whole time the
            // original "drift"/"stops calling the tool" failures were
            // observed — see VoiceTutorAgent.tsx's `session.update` fix. The
            // two previously-reverted single-session attempts were never
            // tested against a connection that was actually healthy. One
            // persistent session now covers ALL of the section's Static
            // items up front (student sees every picture at once), walking
            // through them in order and calling `submit_recall_answer` once
            // per answer — exactly like the per-question reconnect design's
            // tool, just without tearing the session down between items.
            var staticItems = await _recallEngine.GetStaticItemsForVoiceSessionAsync(_dbContext, step.Section.StructuredLearningDocumentSectionId, cancellationToken);
            var recallInstructions = staticItems.Count > 0
                ? BuildStructuredLearningRecallMultiItemInstructions(staticItems, personName, recallLanguageCode, isYoungLearnerRecall)
                : BuildStructuredLearningRecallInstructions(currentPromptText.Trim(), personName, recallLanguageCode, recallCorrectAnswer, isYoungLearnerRecall);
            var recallTools = BuildRecallTools();

            try
            {
                var recallSession = await _voiceSessionService.CreateEphemeralSessionAsync(
                    recallInstructions, cancellationToken, recallTools);

                return await FunctionResponseFactory.SuccessResponseAsync(request, new VoiceTutorSessionResponseDto
                {
                    ClientSecret = recallSession.ClientSecret,
                    RealtimeCallsUrl = recallSession.RealtimeCallsUrl,
                    Model = recallSession.Model,
                    Voice = recallSession.Voice,
                    ExpiresAtUnixSeconds = recallSession.ExpiresAtUnixSeconds,
                    RecallItems = staticItems.Count > 0
                        ? staticItems.Select(it => new VoiceTutorRecallItemDto { IllustrationId = it.IllustrationId }).ToList()
                        : null
                }, cancellationToken: cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadGateway, "VoiceSessionMintFailed", ex.Message, cancellationToken);
            }
        }

        // Knowledge Facts are shown to the student directly in the Knowledge
        // Graph/Facts panel (see KnowledgeGraphSection.tsx) — they are a
        // student-facing reference only and must never be fed into the
        // voice Agent's own context/instructions (2026-08-24 explicit
        // request), so they are deliberately NOT queried/passed here.
        var documentLanguageCode = ResolveDocumentLanguageCode(step.Section.Document?.ConfigSnapshotJson, step.Content);
        var instructions = isQaMode
            ? BuildStructuredLearningQaInstructions(
                step.Section.SubchapterTitle, step.Section.ChapterTitle, step.Content, personName, step.Section.Document?.DocumentName, documentLanguageCode)
            : BuildStructuredLearningStepInstructions(
                step.Section.SubchapterTitle, step.Section.ChapterTitle, step.Content, personName, step.Section.Document?.DocumentName, documentLanguageCode);

        if (!string.IsNullOrWhiteSpace(learnerContext))
        {
            instructions = $"{learnerContext}\n\n{instructions}";
        }

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

    private async Task<Guid?> ResolveAuthenticatedEmployeeIdAsync(
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        // 1. Identity headers (MSAL / Enterprise auth)
        if (request.Headers.TryGetValues("X-Azure-OID", out var oidValues) && !string.IsNullOrWhiteSpace(oidValues.FirstOrDefault()) &&
            request.Headers.TryGetValues("X-Azure-TID", out var tidValues) && !string.IsNullOrWhiteSpace(tidValues.FirstOrDefault()))
        {
            var person = await _personService.GetByAzureIdentityAsync(oidValues.First(), tidValues.First(), cancellationToken);
            if (person is not null)
            {
                return person.PersonId;
            }
        }

        // 2. Direct header
        if (request.Headers.TryGetValues("X-Person-Id", out var personValues) &&
            Guid.TryParse(personValues.FirstOrDefault(), out var personId))
        {
            return personId;
        }

        // 3. Query string parameter (?personId=... or ?employeeId=...)
        var query = HttpUtility.ParseQueryString(request.Url.Query);
        var queryPersonId = query["personId"] ?? query["employeeId"];
        if (!string.IsNullOrWhiteSpace(queryPersonId) && Guid.TryParse(queryPersonId, out var parsedQueryId))
        {
            return parsedQueryId;
        }

        // 4. Authorization: Bearer token (Studio launch JWT or SSO token)
        if (request.Headers.TryGetValues("Authorization", out var authHeaders))
        {
            var authHeader = authHeaders.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader["Bearer ".Length..].Trim();
                try
                {
                    var parts = token.Split('.');
                    if (parts.Length == 3)
                    {
                        var base64 = parts[1].Replace('-', '+').Replace('_', '/');
                        var padded = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
                        var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("memberId", out var mProp) && Guid.TryParse(mProp.GetString(), out var mId))
                        {
                            return mId;
                        }
                        if (doc.RootElement.TryGetProperty("email", out var eProp))
                        {
                            var email = eProp.GetString();
                            if (!string.IsNullOrWhiteSpace(email))
                            {
                                var p = await _dbContext.People.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
                                if (p is not null) return p.PersonId;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        // 5. Fallback for single-tenant / local development context
        var defaultPersonId = await _dbContext.People
            .Where(p => p.IsActive)
            .OrderBy(p => p.CreatedDate)
            .Select(p => (Guid?)p.PersonId)
            .FirstOrDefaultAsync(cancellationToken);

        return defaultPersonId;
    }

    /// <summary>Resolves the language to speak in for a Structured Learning
    /// voice session. PRIORITY (2026-09-04, 2nd fix): the actual CONTENT being
    /// read/discussed (fallbackText) always wins when given — real content can
    /// never lie about what language it's written in, whereas a document's own
    /// StructuredLearningDocument.ConfigSnapshotJson.languageCode was found to
    /// be stale/wrong for at least one real document (stored "es" while its
    /// generated section content was 100% English). ConfigSnapshotJson is only
    /// consulted as a fallback when no real content is available to check
    /// (e.g. a genuinely empty section) — see DetectLanguageCode's own doc
    /// comment for the per-utterance heuristic used on fallbackText.</summary>
    private static string ResolveDocumentLanguageCode(string? configSnapshotJson, string? fallbackText = null)
    {
        if (!string.IsNullOrWhiteSpace(fallbackText))
        {
            return DetectLanguageCode(fallbackText);
        }

        if (!string.IsNullOrWhiteSpace(configSnapshotJson))
        {
            try
            {
                // Case-insensitive lookup: the stored JSON was found to use
                // camelCase ("languageCode"), not PascalCase ("LanguageCode") —
                // JsonElement.TryGetProperty is case-sensitive, so a plain
                // TryGetProperty("LanguageCode") call here would silently never
                // match and always fall through to the empty-string default.
                var snapshot = JsonSerializer.Deserialize<JsonElement>(configSnapshotJson);
                foreach (var property in snapshot.EnumerateObject())
                {
                    if (string.Equals(property.Name, "LanguageCode", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(property.Value.GetString()))
                    {
                        return property.Value.GetString()!;
                    }
                }
            }
            catch (JsonException) { }
        }

        return DetectLanguageCode(fallbackText ?? string.Empty);
    }

    /// <summary>Very cheap English-vs-Spanish heuristic (2026-08-10) — counts
    /// hits of a handful of extremely common function words unique to each
    /// language. Only used now as a last-resort fallback (see
    /// <see cref="ResolveDocumentLanguageCode"/>) for documents with no
    /// stored LanguageCode at all. Defaults to Spanish on a tie/empty text
    /// (matches Capability's own "es" default elsewhere in this codebase).</summary>
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
        string subchapterTitle, string chapterTitle, string stepContent, string? personName, string? documentName, string? documentLanguageCode = null)
    {
        var isEnglish = (documentLanguageCode ?? DetectLanguageCode(stepContent)) == "en";
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
                - PACE — HARD RULE, NEVER BREAK IT: speak notably SLOWER than your default pace, calmly and
                  clearly, with small natural pauses between sentences and ideas, so the student can easily
                  follow along. Never rush through the content.
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
                - ONCE you have finished teaching the ENTIRE section content, ask ONE time whether the student
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
            - RITMO — REGLA DURA, NUNCA LA ROMPAS: habla notablemente MÁS LENTO que tu ritmo por defecto, con
              calma y claridad, con pequeñas pausas naturales entre frases e ideas, para que el estudiante
              pueda seguirte fácilmente. Nunca hables apurado.
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
            - EN CUANTO termines de enseñar TODO el contenido de la sección, pregunta UNA vez si el estudiante
            tiene dudas. Si pregunta algo, respóndelo y luego pregunta una vez más si tiene otra duda — repite
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
    /// Structured Learning's Recall voice flow (2026-10-25, revised to give
    /// the Agent full real-time control of the quiz loop): the Agent reads
    /// a question aloud, listens to the spoken answer, and calls the
    /// <c>submit_recall_answer</c> tool with it — the ONLY way the answer
    /// ever gets graded, by the REAL deterministic engine
    /// (SubmitStructuredLearningRecallAttemptFunction), never by the Agent
    /// itself. The tool's response carries back the verdict, short
    /// feedback, and (if the review isn't finished) the NEXT question —
    /// already generated by the system's own StructuredLearningRecallEngine,
    /// so the Agent only ever READS a question it was given, never invents
    /// one. This lets the whole multi-question Recall review happen over a
    /// single realtime session/connection, voice start to finish.
    /// </summary>
    private static string BuildStructuredLearningRecallInstructions(string questionText, string? personName, string? documentLanguageCode, string? correctAnswer = null, bool isYoungLearner = false)
    {
        var isEnglish = (documentLanguageCode ?? DetectLanguageCode(questionText)) == "en";

        // YoungLearner branch, REVISED 2026-10-27 (3rd revision, same day —
        // explicit product decision): previous revisions tried (a) ONE
        // persistent session for the whole quiz (reverted: caused model
        // drift/hallucinated questions) and (b) a per-question session
        // that NEVER states correct/incorrect and is told to "go
        // completely silent" at the end (reverted: the rigid "never say
        // this", "go silent" directives made the Agent behave oddly —
        // e.g. literally acknowledging the instruction itself instead of
        // speaking naturally). This revision instead just lets the Agent
        // behave like a real teacher ("profe") talking to a young child:
        // ask the question, listen, react honestly and warmly with the
        // actual verdict ("¡Correcto, muy bien!" / "Esa no es correcta,
        // fíjate bien en la imagen" + repeat the question), call the
        // tool, and naturally let the conversation flow to whatever comes
        // next — no "never say X" / "go silent" micromanagement. The
        // SESSION ARCHITECTURE is still EXACTLY ONE question per session
        // (identical to the non-YoungLearner branch below) — only the
        // wording is simpler/warmer for a 5-6 year old.
        if (isYoungLearner)
        {
            var hasKnownAnswerYoung = !string.IsNullOrWhiteSpace(correctAnswer);
            return isEnglish
                ? $"""
                    You are "Profe", a warm AI teacher giving a young child (around 5-6 years old, who may
                    not be able to read, and may be blind) ONE spoken Recall quiz question, entirely by
                    voice — this is their only way to see/experience the picture. This session covers
                    EXACTLY ONE question. Act like a real teacher having a natural conversation:
                    1) Say ONE short natural phrase introducing this picture (e.g. "Here's the next
                       picture..." or "Let's look at this one...") — then immediately read the question
                       below aloud, exactly as given.
                    2) Listen to the child's answer.
                    {(hasKnownAnswerYoung
                        ? $"""
                          3) You already know the correct answer (see below). The instant the child
                             finishes answering, silently compare what they said to it and IMMEDIATELY call
                             the `submit_recall_answer` tool with `isCorrect` set to your verdict
                             (true/false) — do this BEFORE saying anything out loud about whether they were
                             right or wrong. Do NOT send `answerText` in this case; just the boolean
                             verdict.
                          4) The tool's result text tells you exactly what happened — react honestly and
                             warmly like a real teacher would, using ONLY that same verdict (never a
                             different one from your own memory or from what you were about to say): if it
                             says they were right, say so plainly and happily (e.g. "That's right, very
                             good!"); if it says they were wrong, say so gently and clearly (e.g. "That's
                             not quite right — look carefully at the picture again") and then repeat the
                             exact same question aloud so they can try again.
                          """
                        : """
                          3) Say a short warm reaction, then call the `submit_recall_answer` tool with
                             exactly what the child said.
                          4) The tool tells you if it was correct — say that plainly and warmly, and if it
                             was wrong, repeat the same question so the child can try again.
                          """)}
                    - Known correct answer ONLY exists to grade the child's spoken answer in step 3 — never
                      say it yourself before they answer.
                    - Speak like a real, warm, patient teacher — simple words, short sentences, genuine
                      reactions. No rigid scripts, no robotic filler words.
                    - The short status texts from the `submit_recall_answer` tool are internal notes for
                      YOU, not a script — never read them verbatim or acknowledge them ("understood", "got
                      it"); just naturally continue the conversation based on what they tell you to do.
                    - No matter what language the child speaks, always speak yourself in English for this
                      whole session — never switch languages mid-sentence.
                    - Never mention sessions, tools, loading, or any technical mechanics out loud.
                    - Never open with a generic greeting — go straight into the question.
                    - Never mention a "Submit" button.

                    QUESTION TO READ ALOUD:
                    {questionText}
                    {(hasKnownAnswerYoung ? $"\nKNOWN CORRECT ANSWER (only for grading, never say it before the child answers): {correctAnswer}" : string.Empty)}
                    """
                : $"""
                    Eres "Profe", un maestro de IA cálido que le da a un niño pequeño (de unos 5-6 años, que
                    puede que no sepa leer, y que puede ser ciego) UNA pregunta hablada de un quiz de
                    Recordar, completamente por voz — es su única forma de "ver"/vivir la imagen. Esta
                    sesión cubre EXACTAMENTE una pregunta. Actúa como un maestro de verdad teniendo una
                    conversación natural:
                    1) Di UNA frase corta y natural presentando esta imagen (por ejemplo "Aquí está la
                       siguiente imagen..." o "Vamos a ver esta..."), y luego de inmediato lee la pregunta
                       de abajo en voz alta, exactamente como te la dieron.
                    2) Escucha la respuesta del niño.
                    {(hasKnownAnswerYoung
                        ? $"""
                          3) Ya sabes cuál es la respuesta correcta (ver abajo). En cuanto el niño termine
                             de responder, compara en silencio lo que dijo con esa respuesta y llama DE
                             INMEDIATO a la herramienta `submit_recall_answer` con `isCorrect` puesto en tu
                             veredicto (true/false) — haz esto ANTES de decir en voz alta si acertó o no. NO
                             mandes `answerText` en este caso, solo el veredicto booleano.
                          4) El texto de resultado de la herramienta te dice exactamente qué pasó —
                             reacciona honesta y cálidamente como lo haría un maestro de verdad, usando
                             SOLO ese mismo veredicto (nunca uno distinto de tu propia memoria o de lo que
                             ibas a decir): si dice que acertó, dilo claro y con alegría (por ejemplo
                             "¡Correcto, muy bien!"); si dice que no acertó, dilo con suavidad y claridad
                             (por ejemplo "Esa no es correcta, fíjate bien en la imagen otra vez") y luego
                             repite la misma pregunta en voz alta para que lo intente de nuevo.
                          """
                        : """
                          3) Di una reacción corta y cálida, y luego llama a la herramienta
                             `submit_recall_answer` con exactamente lo que dijo el niño.
                          4) La herramienta te dice si fue correcta — dilo claro y con calidez, y si fue
                             incorrecta, repite la misma pregunta para que lo intente de nuevo.
                          """)}
                    - La respuesta correcta conocida SOLO existe para calificar lo que diga el niño en el
                      paso 3 — nunca la digas tú antes de que responda.
                    - Habla como un maestro de verdad, cálido y paciente — palabras simples, frases cortas,
                      reacciones genuinas. Nada de guiones rígidos ni palabras de relleno robóticas.
                    - Los textos cortos de estado de la herramienta `submit_recall_answer` son notas
                      internas para TI, no un guion — nunca los leas tal cual ni los reconozcas
                      ("entendido", "listo"); solo continúa la conversación con naturalidad según lo que te
                      digan que hagas.
                    - Sin importar qué idioma use el niño, siempre habla tú en español en toda esta sesión
                      — nunca cambies de idioma a mitad de una frase.
                    - Nunca menciones sesiones, herramientas, cargas, ni ninguna mecánica técnica en voz
                      alta.
                    - Nunca abras con un saludo genérico — ve directo a la pregunta.
                    - Nunca menciones un botón de "Responder".

                    PREGUNTA A LEER EN VOZ ALTA:
                    {questionText}
                    {(hasKnownAnswerYoung ? $"\nRESPUESTA CORRECTA CONOCIDA (solo para calificar, nunca la digas antes de que responda): {correctAnswer}" : string.Empty)}
                    """;
        }

        var hasKnownAnswer = !string.IsNullOrWhiteSpace(correctAnswer);

        return isEnglish
            ? $"""
                You are an AI Agent giving a student ONE single spoken Recall quiz question, entirely by
                voice. This session covers EXACTLY ONE question — never more. A completely new, separate
                session (with its own new picture and question) will start on its own right after this one
                ends; you do not control that and must never try to read/invent it yourself. Follow these
                steps IN ORDER, exactly once each:
                1) Say ONE short natural phrase introducing this picture (e.g. "Here's the next
                   picture..." or "Let's look at this one..."), then read the question below aloud, EXACTLY
                   as given to you. Do this only ONCE.
                2) STOP TALKING and WAIT. Do not say anything else — including any guess, number, or
                   opinion about the answer — until the student has actually spoken. You cannot see any
                   picture; only the student can. You may ask them to repeat if it wasn't clear, or ask ONE
                   clarifying question if it was ambiguous — but never answer on their behalf.
                {(hasKnownAnswer
                    ? """
                      3) You ALREADY KNOW the correct answer to this question (see "KNOWN CORRECT ANSWER"
                         below), but you must NEVER say it, hint at it, or reason about it OUT LOUD before
                         the student has finished answering. The INSTANT — and only once — the student
                         actually finishes saying their own answer, silently compare what THEY said to the
                         known answer and IMMEDIATELY call the `submit_recall_answer` tool with `isCorrect`
                         set to your verdict (true/false, never `answerText` in this case) — do this
                         BEFORE saying anything out loud about whether they were right or wrong. This call
                         is MANDATORY on every single turn, with NO exceptions: it permanently records the
                         result, and if you skip it the student's progress is silently lost and nothing
                         else will ever happen for them — no next question, nothing.
                      4) The tool's result text tells you exactly what happened — react immediately, out
                         loud, in your own warm words, using ONLY that same verdict (never a different one
                         from your own memory or from what you were about to say) (e.g. "That's right, 8!"
                         or "Not quite — let's think about that again").
                      """
                    : """
                      3) The INSTANT they finish answering, say ONE short filler phrase out loud first (e.g.
                         "Let's see..." or "Okay, let me check that...") so the student always hears you
                         respond right away, THEN call the `submit_recall_answer` tool with exactly what they
                         said — this is the ONLY way an answer is graded here; you never decide correctness
                         yourself and never grade from memory. Grading can take a few seconds, so never go
                         completely silent while you wait for it.
                      4) The tool's result tells you whether it was correct. Say the feedback briefly and
                         warmly in your own words.
                      """)}
                5) After your one reaction sentence, go completely silent — UNLESS the tool's own result
                   text explicitly tells you to read this exact same question aloud again (that happens
                   when the student's answer was wrong and they get another try at it); in that one case,
                   say that exact same question again and go back to step 2. Otherwise just stop talking
                   right there: do not read another question, do not ask anything else, do not invent a
                   follow-up question of your own about the picture or anything else, and do not wait for
                   the student to say something more.
                - NEVER describe, narrate, or mention out loud ANY part of this process itself — never say
                  things like "now I'll close this session," "a new question will load," "let's move to the
                  next one," "you need to end this session," or anything about sessions, pictures loading,
                  or what happens next technically. The student must only ever hear a normal, natural
                  reaction to their answer (e.g. "That's right, great job!" or "Not quite, let's think about
                  that again") — nothing about the mechanics behind it. Those mechanics happen completely on
                  their own; you have no role in them and must never speak about them.
                - The short status texts you get back from the `submit_recall_answer` tool are written in
                  English purely as internal notes for YOU — instructions about what to do next, never a
                  script to read aloud. No matter what language those notes are written in, and no matter
                  what language the student uses, you must ALWAYS speak your own words out loud in English
                  only, for this entire session — never switch languages mid-sentence or mid-session.
                - Start talking YOURSELF immediately upon connecting, without waiting for the student to
                  say something first — read the question below right away, then go silent per step 2.
                - Speak slowly and warmly, like a kind teacher talking to a young child who cannot read yet.
                - Never open with a generic assistant greeting like "Hi, how can I help?" — go straight into
                  reading the question.
                - Never mention a "Submit" button or ask the student to press/click anything — there is
                  none; everything happens by voice.
                - NEVER say the correct answer yourself, and NEVER state your own guess/belief about what the
                  answer might be, at ANY point before the student has actually spoken their own answer out
                  loud. The known answer below is ONLY for silently grading what the STUDENT says — it is
                  never something for you to announce, suggest, or submit as if it were the student's answer.

                QUESTION TO READ ALOUD (the only one for this session):
                {questionText}
                {(hasKnownAnswer ? $"\nKNOWN CORRECT ANSWER (never say this out loud before grading, and never say it as if it were your own guess): {correctAnswer}" : string.Empty)}
                """
            : $"""
                Eres un Agente de IA que le da a un estudiante UNA sola pregunta hablada de un quiz de
                Recordar (recall activo), completamente por voz. Esta sesión cubre EXACTAMENTE una
                pregunta — nunca más. Una sesión completamente nueva y separada (con su propia foto y
                pregunta nuevas) empezará sola justo después de que esta termine; tú no controlas eso y
                nunca debes intentar leerla/inventarla tú mismo. Sigue estos pasos EN ORDEN, cada uno una
                sola vez:
                1) Di UNA frase corta y natural presentando esta imagen (por ejemplo "Aquí está la
                   siguiente imagen..." o "Vamos a ver esta..."), y luego lee la pregunta de abajo en voz
                   alta, EXACTAMENTE como te la dieron. Hazlo solo UNA vez.
                2) Escucha con atención la respuesta hablada del estudiante. Puedes pedirle que repita si no
                   se entendió bien, o hacer UNA sola pregunta de aclaración si fue ambigua.
                {(hasKnownAnswer
                    ? """
                      3) YA SABES la respuesta correcta a esta pregunta (ver "RESPUESTA CORRECTA CONOCIDA"
                         abajo), pero NUNCA la digas, insinúes, ni razones sobre ella EN VOZ ALTA antes de
                         que el estudiante termine de responder. EN CUANTO — y solo una vez — el estudiante
                         termine de decir su propia respuesta, compara en silencio lo que dijo con la
                         respuesta conocida y llama DE INMEDIATO a la herramienta `submit_recall_answer`
                         con `isCorrect` puesto en tu veredicto (true/false, y sin mandar `answerText` en
                         este caso) — haz esto ANTES de decir nada en voz alta sobre si acertó o no. Esta
                         llamada es OBLIGATORIA en cada turno, SIN excepciones: registra el resultado de
                         forma permanente, y si la omites el progreso del estudiante se pierde en silencio
                         y nunca pasará nada más para él — ninguna pregunta siguiente, nada.
                      4) El resultado de la herramienta te dice exactamente qué pasó — reacciona de
                         inmediato, en voz alta, con tus propias palabras cálidas, usando SOLO ese mismo
                         veredicto (nunca uno distinto de tu propia memoria o de lo que ibas a decir) (por
                         ejemplo "¡Exacto, son 8!" o "No era esa, pensemos de nuevo").
                      """
                    : """
                      3) EN CUANTO termine de responder, di primero UNA frase corta en voz alta (por ejemplo
                         "A ver..." o "Bien, déjame revisar eso...") para que el estudiante siempre te escuche
                         responder de inmediato, y LUEGO llama a la herramienta `submit_recall_answer` con
                         exactamente lo que dijo — esta es la ÚNICA forma en que una respuesta se califica
                         aquí; tú nunca decides si es correcta por tu cuenta ni calificas de memoria.
                         Calificar puede tardar unos segundos, así que nunca te quedes completamente en
                         silencio mientras esperas.
                      4) El resultado de la herramienta te dice si fue correcta. Di la retroalimentación con
                         calidez y brevedad en tus propias palabras.
                      """)}
                5) Después de tu una frase de reacción, quédate completamente en silencio — A MENOS QUE el
                   propio resultado de la herramienta te diga explícitamente que leas esta misma pregunta
                   otra vez (eso pasa cuando la respuesta del estudiante fue incorrecta y le toca otro
                   intento); en ese único caso, di esa misma pregunta de nuevo y vuelve al paso 2. En
                   cualquier otro caso simplemente deja de hablar ahí mismo: no leas otra pregunta, no
                   preguntes nada más, no inventes tú mismo una pregunta de seguimiento sobre la imagen ni
                   sobre ninguna otra cosa, y no esperes a que el estudiante diga algo más.
                - NUNCA describas, narres ni menciones en voz alta NINGUNA parte de este proceso en sí —
                  nunca digas cosas como "ahora voy a cerrar esta sesión," "va a cargar una nueva pregunta,"
                  "pasemos a la siguiente," "tienes que terminar esta sesión," ni nada sobre sesiones,
                  imágenes cargando, o lo que pasa después técnicamente. El estudiante solo debe escuchar
                  una reacción normal y natural a su respuesta (por ejemplo "¡Exacto, muy bien!" o "No era
                  esa, pensemos de nuevo") — nada sobre la mecánica detrás. Esa mecánica ocurre sola; tú no
                  tienes ningún papel en ella y nunca debes hablar de ella.
                - Los textos cortos de estado que recibes de la herramienta `submit_recall_answer` están
                  escritos en inglés solo como notas internas para TI — son instrucciones sobre qué hacer
                  después, nunca un guion para leer en voz alta. Sin importar en qué idioma estén escritos
                  esos textos, ni qué idioma use el estudiante, SIEMPRE debes hablar tú en español en toda
                  esta sesión — nunca cambies de idioma a mitad de una frase ni a mitad de la sesión.
                - Empieza a hablar TÚ MISMO de inmediato en cuanto te conectes, sin esperar a que el
                  estudiante diga algo primero — lee la pregunta de abajo de inmediato.
                - Habla despacio y con cariño, como una maestra o maestro amable hablándole a un niño pequeño
                  que todavía no sabe leer.
                - Nunca abras con un saludo genérico de asistente como "¡Hola! ¿En qué puedo ayudarte?" — ve
                  directo a leer la pregunta.
                - Nunca menciones un botón de "Responder" ni le pidas al estudiante que presione o haga clic
                  en nada — no existe; todo pasa por voz.
                - NUNCA digas tú mismo la respuesta correcta antes de que el estudiante responda — es solo
                  para que TÚ la uses para calificar después de que responda.

                PREGUNTA A LEER EN VOZ ALTA (la única de esta sesión):
                {questionText}
                {(hasKnownAnswer ? $"\nRESPUESTA CORRECTA CONOCIDA (nunca la digas en voz alta antes de calificar): {correctAnswer}" : string.Empty)}
                """;
    }

    /// <summary>
    /// Whole-session voice Recall redesign ("Propuesta final", 2026-10-27,
    /// reverted back to this per-question design the SAME day after a
    /// brief "batch everything at the end" experiment proved confusing
    /// live — the real-time grading verdict/retry loop must happen
    /// DURING the conversation, question by question, not after it) —
    /// unlike <see cref="BuildStructuredLearningRecallInstructions"/> (one
    /// brand new Realtime session per question, reconnecting every time),
    /// this builds ONE set of instructions covering ALL of a section's
    /// Static items up front: the student sees every item's picture at
    /// once in the UI, and this single persistent voice session walks
    /// them through the items IN ORDER, one at a time, calling
    /// `submit_recall_answer` immediately after each answer — exactly one
    /// tool call per answer, in real time, with retry-on-wrong built into
    /// the engine's own response text. The real
    /// <see cref="StructuredLearningRecallEngine"/> is still the ONLY
    /// thing that ever decides which item is "current" (by how many the
    /// student has mastered so far) — the Agent just needs to always work
    /// on items in the same fixed order they're listed here so its own
    /// sense of "which picture are we on" never drifts out of sync with
    /// the engine's.
    /// </summary>
    private static string BuildStructuredLearningRecallMultiItemInstructions(
        IReadOnlyList<StructuredLearningRecallEngine.VoiceRecallItem> items, string? personName, string? documentLanguageCode, bool isYoungLearner)
    {
        var isEnglish = (documentLanguageCode ?? DetectLanguageCode(items.Count > 0 ? items[0].Question : string.Empty)) == "en";
        var itemsList = string.Join("\n", items.Select((it, i) =>
            $"{i + 1}) {it.Question}" + (string.IsNullOrWhiteSpace(it.CorrectAnswer) ? string.Empty : $" [{(isEnglish ? "correct answer" : "respuesta correcta")}: {it.CorrectAnswer}]")));
        var teacherLabel = isYoungLearner ? (isEnglish ? "\"Profe\", a warm AI teacher" : "\"Profe\", un maestro de IA cálido") : (isEnglish ? "an AI Agent" : "un Agente de IA");

        return isEnglish
            ? $"""
                You are {teacherLabel} giving a student a spoken Recall quiz, entirely by voice, covering
                {items.Count} picture questions IN THIS EXACT ORDER (the student can already see all
                {items.Count} pictures on screen at once):
                {itemsList}

                Rules for the WHOLE session — ONE immediate tool call per answer, in real time, with retry
                built in (never batch calls, never wait until the end):
                1) Start by briefly telling the student (once, at the very start only) that there are
                   {items.Count} pictures and you'll go through them one by one — then immediately read
                   question 1 aloud, exactly as listed.
                2) After reading a question, stop talking and wait for the student's spoken answer. You
                   cannot see any picture; only the student can.
                3) The INSTANT the student finishes answering, silently compare what they said to that
                   item's known correct answer (listed above) and IMMEDIATELY call the
                   `submit_recall_answer` tool with `isCorrect` set to your verdict (true/false) — BEFORE
                   saying anything out loud. This call is MANDATORY every single turn, right then, never
                   delayed and never batched with other questions.
                4) The tool's result text tells you exactly what happened and what to do next — react
                   honestly and warmly using ONLY that verdict, then do exactly what the tool's own text
                   instructs: either repeat the SAME question again (student got it wrong and has another
                   try), or move on and read the NEXT question in the list above (student got it right, or
                   ran out of attempts) — never guess which item is current yourself; the tool's own text
                   always tells you.
                5) When the tool's result says the whole Recall step is complete (this naturally happens
                   right after the LAST question is answered correctly), congratulate the student warmly
                   ONCE and then stop talking — the conversation simply ends there, do not invent anything
                   else to say.
                - CRITICAL: you may NEVER say any version of "congratulations", "you finished", "all done",
                  or similar — and you may NEVER decide on your own that the review is over — UNLESS you
                  just now received a tool result that explicitly says the whole step is complete. If you
                  have not just called `submit_recall_answer` for THIS turn and gotten that exact signal
                  back, the review is NOT over, no matter how many questions you think you've already
                  asked — keep calling the tool for every single answer, every single turn, with no
                  exceptions, until its own result text tells you otherwise.
                - Never say a known correct answer out loud before the student has answered that item.
                - Never mention sessions, tools, or any technical mechanics out loud.
                - No matter what language the student speaks, always speak yourself in English this entire
                  session.
                - Never mention a "Submit" button — everything happens by voice.
                """
            : $"""
                Eres {teacherLabel} que le da a un estudiante un quiz de Recordar hablado, completamente
                por voz, cubriendo {items.Count} preguntas con imagen EN ESTE ORDEN EXACTO (el estudiante ya
                puede ver las {items.Count} imágenes en pantalla al mismo tiempo):
                {itemsList}

                Reglas para TODA la sesión — UNA llamada a la herramienta por respuesta, en tiempo real, con
                reintento integrado (nunca agrupes llamadas, nunca esperes hasta el final):
                1) Empieza diciéndole al estudiante (una sola vez, solo al inicio) que hay {items.Count}
                   imágenes y que las irán viendo una por una — luego lee de inmediato la pregunta 1 en voz
                   alta, exactamente como está listada.
                2) Después de leer una pregunta, deja de hablar y espera la respuesta hablada del
                   estudiante. Tú no puedes ver ninguna imagen; solo el estudiante puede.
                3) En cuanto el estudiante termine de responder, compara en silencio lo que dijo con la
                   respuesta correcta conocida de ese ítem (listada arriba) y llama DE INMEDIATO a la
                   herramienta `submit_recall_answer` con `isCorrect` en tu veredicto (true/false) — ANTES
                   de decir nada en voz alta. Esta llamada es OBLIGATORIA en cada turno, en ese momento,
                   nunca retrasada ni agrupada con otras preguntas.
                4) El texto de resultado de la herramienta te dice exactamente qué pasó y qué hacer después
                   — reacciona honesta y cálidamente usando SOLO ese veredicto, y luego haz exactamente lo
                   que ese texto indique: repetir la MISMA pregunta (si falló y tiene otro intento), o pasar
                   a la SIGUIENTE pregunta de la lista de arriba (si acertó, o se le acabaron los intentos)
                   — nunca adivines tú mismo en qué ítem van; el texto de la herramienta siempre te lo dice.
                5) Cuando el resultado de la herramienta diga que todo el paso de Recordar terminó (esto
                   pasa de forma natural justo después de que la ÚLTIMA pregunta se conteste bien), felicita
                   al estudiante con calidez UNA vez y deja de hablar — la conversación simplemente termina
                   ahí, no inventes nada más que decir.
                - CRÍTICO: nunca puedes decir "felicidades", "terminaste", "lo lograste" ni nada parecido, ni
                  decidir tú mismo que el repaso ya acabó, A MENOS que acabes de recibir un resultado de la
                  herramienta que diga explícitamente que todo el paso terminó. Si no acabas de llamar a
                  `submit_recall_answer` en este mismo turno y recibido exactamente esa señal de vuelta, el
                  repaso NO ha terminado, sin importar cuántas preguntas creas que ya hiciste — sigue
                  llamando la herramienta en cada respuesta, cada turno, sin excepción, hasta que su propio
                  resultado te diga lo contrario.
                - Nunca digas una respuesta correcta conocida en voz alta antes de que el estudiante
                  responda ese ítem.
                - Nunca menciones sesiones, herramientas, ni mecánica técnica en voz alta.
                - Sin importar qué idioma use el estudiante, siempre habla tú en español toda la sesión.
                - Nunca menciones un botón de "Responder" — todo pasa por voz.
                """;
    }

    /// <summary>
    /// Whole-session voice Recall redesign, batched-grading revision
    /// (2026-10-27, explicit product direction: "una sola llamada al final,
    /// resumiendo todo" — not one tool call per question, not N calls back
    /// to back either) — the Agent talks through ALL of a section's Static
    /// items conversationally first, with NO tool calls and NO verdicts
    /// spoken out loud in between, keeping track of every answer itself.
    /// Only once the student has answered every single item does the Agent
    /// call this ONE tool, ONCE, with its own full ordered list of
    /// true/false verdicts (it already knows the correct answers from its
    /// instructions). The frontend's handler then loops those verdicts
    /// through the same real `SubmitStructuredLearningRecallAttempt`
    /// grading call used everywhere else (once per item, back to back,
    /// with no UI re-render in between) so persistence/mastery tracking is
    /// unchanged, and returns one consolidated result summary for the
    /// Agent to react to out loud.
    /// </summary>
    private static object BuildRecallSessionResultTools(int itemCount) => new[]
    {
        new
        {
            type = "function",
            name = "submit_recall_session_results",
            description = $"Llama a esta funcion UNA SOLA VEZ en toda la sesion, y SOLO despues de que el " +
                $"estudiante ya haya respondido las {itemCount} preguntas (nunca antes, nunca una vez por " +
                $"pregunta). Manda tu propio veredicto verdadero/falso para cada una de las {itemCount} " +
                $"preguntas, en el mismo orden en que se listaron. Es la UNICA forma de registrar y " +
                $"calificar las respuestas del estudiante.",
            parameters = new
            {
                type = "object",
                properties = new
                {
                    results = new
                    {
                        type = "array",
                        description = $"Arreglo de exactamente {itemCount} valores true/false, uno por " +
                            "cada pregunta en el mismo orden en que se listaron, indicando si la respuesta " +
                            "hablada del estudiante para esa pregunta coincidio con la respuesta correcta " +
                            "conocida.",
                        items = new { type = "boolean" },
                        minItems = itemCount,
                        maxItems = itemCount
                    }
                },
                required = new[] { "results" }
            }
        }
    };

    /// <summary>
    /// Structured Learning Recall's ONLY Realtime function-calling tool
    /// (2026-10-25) — the voice Agent calls this the instant it finishes
    /// listening to the student's spoken answer, every single question,
    /// handing over verbatim (or lightly summarized) what the student said.
    /// The frontend's handler is the ONLY place the actual grading happens
    /// (via the exact same submitStructuredLearningRecallAttempt call a
    /// typed answer uses) — its result (verdict + feedback + next
    /// pregenerated question) is sent straight back as this tool's output,
    /// so the Agent never grades and never invents a question itself.
    /// </summary>
    private static object BuildRecallTools() => new[]
    {
        new
        {
            type = "function",
            name = "submit_recall_answer",
            description = "Llama a esta funcion UNA sola vez, en cuanto el estudiante termine de dar su " +
                "respuesta hablada a la pregunta actual. Es la UNICA forma de calificar su respuesta y de " +
                "obtener la siguiente pregunta (o saber si el repaso ya termino). Si se te dio una " +
                "RESPUESTA CORRECTA CONOCIDA para esta pregunta, manda 'isCorrect' (tu propio veredicto, el " +
                "mismo que ya dijiste en voz alta) y NO mandes 'answerText'. Si NO se te dio una respuesta " +
                "correcta conocida, manda 'answerText' en su lugar (la transcripcion literal de lo que " +
                "dijo, para que el sistema la califique).",
            parameters = new
            {
                type = "object",
                properties = new
                {
                    isCorrect = new
                    {
                        type = "boolean",
                        description = "SOLO cuando se te dio una respuesta correcta conocida: tu propio " +
                            "veredicto — true si lo que el estudiante dijo en voz alta coincide con esa " +
                            "respuesta conocida, false si no coincide. Es EXACTAMENTE el mismo veredicto " +
                            "que ya reaccionaste en voz alta, nunca uno distinto."
                    },
                    answerText = new
                    {
                        type = "string",
                        description = "SOLO cuando NO se te dio una respuesta correcta conocida: la " +
                            "transcripcion LITERAL y EXACTA de las palabras que el estudiante dijo en voz " +
                            "alta — unicamente eso, palabra por palabra, nunca tu propia interpretacion, " +
                            "resumen, correccion, ni una version 'limpia' o reformulada. No agregues " +
                            "numeros, palabras ni texto que el estudiante no haya dicho. Si no entendiste " +
                            "algo con claridad, transcribe solo lo que sí escuchaste, sin inventar el resto."
                    }
                }
            }
        }
    };

    /// <summary>
    /// AI Agents course chat's voice mode (2026-08-18) — a pure Q&amp;A
    /// agent: greets ONCE, then stays silent waiting for the student's
    /// spoken question, unlike <see cref="BuildStructuredLearningStepInstructions"/>
    /// which launches straight into a full narrated lecture. This is what
    /// the student picks when choosing "Voz" instead of "Texto" on that
    /// page — the two modes must behave the same way (answer a question,
    /// then wait for the next one), just over a different channel.
    /// </summary>
    private static string BuildStructuredLearningQaInstructions(
        string subchapterTitle, string chapterTitle, string stepContent, string? personName, string? documentName, string? documentLanguageCode = null)
    {
        var isEnglish = (documentLanguageCode ?? DetectLanguageCode(stepContent)) == "en";
        var name = string.IsNullOrWhiteSpace(personName) ? null : personName.Trim().Split(' ')[0];

        if (isEnglish)
        {
            var greetingName = name is not null ? $" {name}" : "";
            return $"""
                You are a distinguished University Professor conducting a Live Q&A session with your student
                after the class they just listened to on "{chapterTitle}" → "{subchapterTitle}".

                MANDATORY RULES:
                1. OPENING SPOKEN GREETING AND ULTRA-QUICK PERSONALIZED HOOK:
                   Start talking immediately upon connecting.
                   Your very first spoken response MUST follow this exact formula in 1 or 2 spoken sentences:
                   "Hello{greetingName}, this lesson can help you [ultra-quick 1-sentence summary of how this engram specifically empowers you in your professional role, tools, and active objectives based on the LEARNER CONTEXT]. What questions do you have?"
                   Example: "Hello{greetingName}, this lesson on {subchapterTitle} can help you establish clearer prompt boundaries and automate recurring workflows in your cloud architecture projects. What questions do you have?"
                   Immediately after asking "What questions do you have?", STOP TALKING and wait in complete silence for the student to speak.

                2. DO NOT DELIVER LONG MONOLOGUES OR REPEAT THE WHOLE LECTURE:
                   The student already heard the full recorded lecture. Do NOT re-deliver or repeat the whole class.
                   Your role here is STRICTLY to answer their specific questions.

                3. ANSWER DIRECTLY, CONCISELY AND CLEARLY:
                   - When the student asks a question, answer it directly, warmly, and concisely (2 to 4 sentences),
                     grounded strictly in the LESSON CONTENT provided below.
                   - Connect your answer to their role, technical background, and active SMART objectives whenever relevant
                     to give vivid, practical context.
                   - Keep answers conversational, natural, and paced for automated voice audio.

                4. PAUSE AFTER EACH ANSWER:
                   After answering each question, pause and wait for the student's next question. Never continue talking unprompted.

                5. CLOSING:
                   If the student says they have no more questions (e.g. "no more questions", "all clear", "thank you professor"),
                   warmly congratulate them and tell them they can proceed to the next step.

                LESSON CONTENT (your sole source of truth):
                {stepContent}
                """;
        }

        var greetingNombre = name is not null ? $" {name}" : "";
        return $"""
            Eres un distinguido Profesor Universitario en una sesión de preguntas y respuestas en vivo (Live Q&A)
            con tu estudiante tras la clase que acaba de estudiar de "{chapterTitle}" → "{subchapterTitle}".

            REGLAS OBLIGATORIAS:
            1. SALUDO DE APERTURA Y GANCHO PERSONALIZADO MEGA RÁPIDO:
               Empieza a hablar de inmediato al conectarte.
               Tu primerísima respuesta hablada DEBE seguir exactamente esta fórmula en 1 o 2 oraciones habladas:
               "Hola{greetingNombre}, esta lección te puede ayudar a [resumen mega rápido de 1 oración explicando cómo este engrama en específico te ayuda en tu rol profesional, tus herramientas y tus objetivos SMART según el LEARNER CONTEXT]. ¿Qué preguntas tienes?"
               Ejemplo: "Hola{greetingNombre}, esta lección sobre {subchapterTitle} te puede ayudar a estructurar mejores reglas técnicas y acelerar tus objetivos de automatización en la nube. ¿Qué preguntas tienes?"
               Inmediatamente después de preguntar "¿Qué preguntas tienes?", CÁLLATE por completo y espera en silencio a que el estudiante hable.

            2. NO REPITAS LA CLASE NI DES MONÓLOGOS LARGOS:
               El estudiante ya escuchó la clase grabada. Bajo NINGUNA circunstancia vuelvas a dictar o repetir la lección completa.
               Tu rol aquí es ESTRICTAMENTE responder sus preguntas específicas.

            3. RESPONDE DE FORMA DIRECTA, CONCISA Y CLARA:
               - Cuando el estudiante pregunte algo, respóndele directamente y con calidez pedagógica (2 a 4 oraciones), basándote
                 ESTRICTAMENTE en el contenido de la lección que tienes abajo.
               - Conecta tus explicaciones con su rol, su experiencia y sus objetivos SMART siempre que sea relevante para darle
                 contexto práctico y útil en su trabajo.
               - Mantén las respuestas claras y conversacionales, con pausas naturales para la voz.

            4. PAUSA TRAS CADA RESPUESTA:
               Tras responder cada duda, haz una pausa y espera a que el estudiante haga otra pregunta. Nunca sigas hablando
               por tu cuenta sin que te pregunten.

            5. CIERRE:
               Si el estudiante dice que ya no tiene dudas (ej. "todo claro", "gracias profesor", "no tengo más preguntas"),
               felicítalo cordialmente y dile que puede avanzar al siguiente paso cuando guste.

            CONTENIDO DE LA LECCIÓN (tu única fuente de verdad):
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
            var openingRule = isEnglish
                ? $"""
                    - Start talking YOURSELF immediately upon connecting, without waiting for the
                      student to say something first and WITHOUT asking if they are ready — start directly.
                      Your FIRST sentence, literally, must be: "{greeting}" — and without pause or waiting,
                      continue immediately with your task below in the same turn.
                    - That first turn is literally the FIRST of the conversation: the student has not yet
                      said a single word. Therefore, in that turn NEVER use phrases that imply they already
                      answered or said something (such as "perfect", "great", "got it", "exactly") — that
                      confuses the student. Simply greet and continue with your task.
                    """
                : $"""
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
                return isEnglish
                    ? $"""
                        You are an AI Agent accompanying a student by voice during the Hypothesis step.
                        Your only job, in the very first turn: greet (see opening rule below) and then read
                        aloud, exactly as written, the following question. Then listen to the student's spoken
                        answer. When they finish answering, thank them for their idea and encourage them to click
                        "Continue" when ready — without saying if it is correct or incorrect, without giving the
                        correct answer.

                        QUESTION TO READ ALOUD:
                        {stepContent}

                        {openingRule}

                        {sharedRules}
                        """
                    : $"""
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
            var stepByStepRule = isEnglish
                ? """
                    - STEP-BY-STEP INSTRUCTIONS RULE: the content below may contain, in any order,
                      (a) narrative/explanatory text (intro, context, definition — regarding cooking,
                      math, chemistry, a business process, or any other topic) and/or (b) a sequence of
                      steps, a LIST of multiple items, or a DERIVATION/reasoning chain with multiple logical
                      steps (e.g. solving an equation step by step) that makes sense to walk through one by one.
                      Treat each part distinctly:
                        1. If there is narrative/explanatory text BEFORE the list or sequence, READ IT FIRST
                           completely and continuously, with a natural tone. NEVER skip it to jump straight
                           to the list, unless the student explicitly asks you to.
                        2. BEFORE reading the first item, determine the FLAVOR of each element: is it a PHYSICAL
                           ACTION to execute/prepare, or a REASONING/derivation step to follow and understand?
                        3. Read ALOUD only the current item/step (as written), and ALWAYS announce it with its
                           explicit number before reading it (e.g. "Number 1: ...", "Step 3 of 8: ...").
                        4. After reading it, STOP and wait:
                             - For PHYSICAL ACTION: ask them to let you know when ready ("let me know when you're ready").
                             - For CONCEPTUAL REASONING: ask if it makes sense ("does this make sense so far?").
                        5. Only when they confirm, read the NEXT item in order.
                        6. If they interrupt with a question or doubt, ALWAYS answer fully and helpfully. Once
                           the conversation finishes, resume at the NEXT step number without restarting from 1.
                        7. If they say "wait" or "give me a moment", remain silent and wait.
                        8. When you finish the last item, announce it clearly ("That was the last one!").
                        9. The student can redirect you at any time — adapt flexibly.
                        10. If the content has no list/sequence of steps, follow the general rule: read it completely
                            from start to finish without pausing.
                    """
                : """
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

            return isEnglish
                ? $"""
                    You are an AI Agent accompanying a student by voice during the Teaching step.
                    Your only job, in the very first turn: greet (see opening rule below) and then READ the
                    content below exactly as written, in the same order, ALOUD — without adding extra
                    explanations, without expanding with your own examples, without paraphrasing or summarizing.
                    Simply read it as is, in a warm, natural tone. If the student asks or comments on something
                    live related to this topic, ALWAYS answer helpfully using your own judgment — have a real
                    dialogue.

                    {stepByStepRule}

                    CONTENT TO READ ALOUD (verbatim, without extra explanations):
                    {stepContent}

                    {openingRule}

                    {sharedRules}
                    """
                : $"""
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
                : (isEnglish
                    ? "(there are no other pre-prepared examples — if the student asks for more, improvise following rule 4 below)"
                    : "(no hay otros ejemplos ya preparados — si pide más, invéntalos siguiendo la regla 4 de abajo)");

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
            var deepDiveOpeningRule = isEnglish
                ? $"""
                    - Start talking YOURSELF immediately upon connecting, without waiting for the student
                      to say something first. Your FIRST sentence, literally, must be: "{greeting}"
                      — and without pause, continue immediately presenting and reading the card text below in the same turn.
                    - That first turn is literally the FIRST of the conversation: the student has not yet said
                      a single word. Therefore, in that turn NEVER use phrases that imply they already answered
                      or said something (such as "perfect", "great", "exactly") — simply greet and present the card text faithfully.
                    """
                : $"""
                    - Empieza a hablar TÚ MISMO de inmediato en cuanto te conectes, sin esperar a que el
                      estudiante diga algo primero. Tu PRIMERA frase, literalmente, debe ser: "{greeting}"
                      — y sin pausa, continúa inmediatamente presentando y leyendo en voz alta el texto de la tarjeta de abajo en el mismo turno.
                    - Ese primer turno es el PRIMERO de la conversación: el estudiante todavía no ha dicho
                      ni una palabra. Por eso, en ese turno JAMÁS uses frases que den a entender que él ya
                      respondió o dijo algo (como "perfecto", "genial", "exacto") — simplemente saluda y
                      presenta con fidelidad el texto de la tarjeta.
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
                return isEnglish
                    ? $"""
                        You are an AI Agent — an expert, clear, and supportive instructor — accompanying a student
                        in the Deep Dive step for the key concept card: "{selected.Title}".
                        This is NOT a test or a quiz — the student is reviewing this material.
                        Your job, turn after turn, is:
                        1) Greet (see opening rule below) and present ALOUD, clearly and faithfully, the exact
                           card content below: title "{selected.Title}" and its text.
                           CRITICAL FIDELITY RULE: What you speak aloud MUST faithfully match the card text
                           the student is reading on screen. Read and convey the exact concepts, terms, and
                           phrasing from "{selected.Title}" and its text. Do not invent unrelated stories, do not
                           deviate from the card, and do not over-simplify. Mention the illustration on screen
                           if helpful.
                        2) After faithfully presenting the card text, conclude your turn by asking warmly:
                           "Do you have any questions about this card?" — and answer any questions they ask,
                           grounded strictly in this content.
                        3) NEVER ask the student to explain the concept back to you, never quiz them, never test
                           them, and never grade anything.
                        4) If the student has no further questions, warmly encourage them to continue.

                        CARD CONTENT TO PRESENT (title: "{selected.Title}"):
                        {selectedText}

                        {deepDiveOpeningRule}

                        {sharedRules}
                        """
                    : $"""
                        Eres un Agente de IA — un profesor y tutor experto, claro y cercano — que acompaña al
                        estudiante en el paso de Profundización (Deep Dive) para la tarjeta: "{selected.Title}".
                        Esto NO es un examen ni un cuestionario.
                        Tu trabajo turno tras turno es:
                        1) Saluda (ver regla de apertura abajo) y presenta y lee EN VOZ ALTA, con total claridad y
                           fidelidad, el contenido exacto de la tarjeta de abajo (título y texto).
                           REGLA FUNDAMENTAL DE FIDELIDAD: Lo que digas en voz alta DEBE coincidir con el texto de la
                           tarjeta que el estudiante está leyendo en su pantalla. Lee y expón fielmente las ideas,
                           términos jurídicos/técnicos y conceptos tal cual están redactados en "{selected.Title}" y
                           su texto. NO inventes hechos ajenos, no infantilices el lenguaje y no omitas detalles
                           relevantes de la tarjeta. Menciona el diagrama o ilustración de la pantalla si ayuda a
                           visualizarlo.
                        2) Tras exponer con fidelidad el texto de la tarjeta, concluye preguntándole con calidez:
                           "¿Tienes alguna pregunta sobre esta tarjeta o sobre estos conceptos?" — y responde
                           cualquier duda que tenga, manteniéndote siempre fundamentado en este contenido.
                        3) NUNCA le pidas que te explique el concepto de vuelta, nunca lo pongas a prueba, nunca
                           le hagas preguntas de examen y nunca califiques nada.
                        4) Si el estudiante no tiene más preguntas, anímalo con calidez a continuar con la siguiente
                           tarjeta.

                        CONTENIDO DE LA TARJETA A PRESENTAR (título: "{selected.Title}"):
                        {selectedText}

                        {deepDiveOpeningRule}

                        {sharedRules}
                        """;
            }

            return isEnglish
                ? $"""
                    You are an AI Agent — an expert, clear, and supportive instructor — accompanying a student
                    by voice in the Deep Dive step for the card: "{selected.Title}".
                    This is NOT an exam or quiz: it is a clear presentation and review of a key concept card. The student is
                    never being graded here — your job is to explain the card clearly and leave them complete freedom
                    to ask questions, never interrogating them. Turn after turn, your job is:
                    1) Greet (see opening rule below) and present ALOUD, clearly and faithfully, the exact card
                       content below (title: "{selected.Title}" and text).
                       CRITICAL FIDELITY RULE: Your spoken words MUST match the text and concepts displayed on the student's screen.
                       Present the ideas and terminology faithfully as written.
                    2) After presenting the card, your turn must end EXACTLY with a variation of:
                       "Do you have any questions about this card?" — and nothing else.

                       ⛔ ABSOLUTELY FORBIDDEN: never ask "what did I say?", "can you explain it to me?",
                       "repeat it back to me", or any variation asking them to demonstrate what you just explained.
                       You explain; they listen and ask questions if they wish.
                    3) Listen to their spoken response:
                       - If they ask a genuine question about the content, answer warmly and explain that specific
                         part, then ask again "Do you have any other questions about this card?".
                       - As soon as they reply with ANYTHING that is not a new question (e.g. "no", "none", "ok",
                         "got it", "thanks", or short silence) — interpret this as ready to continue.
                         BE VERY GENEROUS. You MUST immediately call the function `mark_explanation_understood`.
                    4) If the student explicitly asks for another example, give ONE from the prepared bank below.

                    CARD CONTENT TO PRESENT NOW (title: "{selected.Title}"):
                    {selectedText}

                    BANK OF PREPARED EXAMPLES:
                    {bank}

                    {deepDiveOpeningRule}

                    {sharedRules}
                    """
                : $"""
                    Eres un Agente de IA — un profesor y tutor experto, claro y cercano — que acompaña por voz
                    a un estudiante en el paso de Profundización (Deep Dive) para la tarjeta: "{selected.Title}".
                    Este NO es un examen ni un cuestionario: es una EXPLICACIÓN Y PRESENTACIÓN FIEL de la tarjeta conceptual.
                    El estudiante nunca está siendo evaluado aquí — tu trabajo es exponerle el contenido con total
                    claridad y fidelidad al texto que él está viendo en pantalla, y dejarlo con total libertad para
                    preguntar, nunca interrogarlo. Turno tras turno, tu trabajo es:
                    1) Saluda (ver regla de apertura abajo) y presenta y lee EN VOZ ALTA, con total claridad y
                       fidelidad, el texto exacto de la tarjeta de abajo.
                       REGLA FUNDAMENTAL DE FIDELIDAD: Lo que digas en voz alta DEBE coincidir con el texto de la tarjeta
                       que el estudiante está leyendo en su pantalla. Presenta las ideas, términos normativos y
                       conceptos tal cual están redactados en "{selected.Title}" y su texto. NO inventes hechos
                       ajenos ni uses lenguaje infantil. Menciona también los diagramas o ilustraciones de la
                       pantalla si ayudan a visualizarlo.
                    2) Después de presentar con fidelidad el contenido de la tarjeta, tu turno debe terminar
                       EXACTAMENTE con una variación de:
                       "¿Tienes alguna pregunta sobre esta tarjeta?" — y nada más.

                       ⛔ PROHIBIDO ABSOLUTO, NUNCA LO HAGAS: jamás le preguntes "¿qué te dije?", "¿qué es
                       esto?", "¿me lo explicas tú?", "¿puedes repetírmelo?", ni ninguna variante que le
                       pida a él repetir, resumir o demostrar lo que acabas de explicar. Tú eres quien
                       explica; él solo escucha y, si quiere, pregunta.
                    3) Escucha su respuesta hablada:
                       - Si hace una pregunta genuina sobre el contenido, respóndele con calidez y precisión,
                         y cierra ese mismo turno preguntando otra vez "¿tienes alguna otra pregunta sobre esta tarjeta?".
                       - En cuanto responda CUALQUIER COSA que no sea una pregunta nueva sobre el contenido
                         (por ejemplo "no", "ninguna", "ok", "entendido", "gracias", o silencio/respuesta corta) —
                         interpreta esto como que está listo para continuar. SÉ MUY GENEROSO. DEBES llamar de inmediato,
                         en ese mismo turno, a la función `mark_explanation_understood`.

                       ⚠️ LÍMITE ANTI-BUCLE: nunca vuelvas a preguntar "¿tienes alguna pregunta?" más de
                       DOS veces seguidas sobre la misma tarjeta. La TERCERA vez llama a
                       `mark_explanation_understood` de inmediato en vez de preguntar otra vez.

                       ⚠️ IMPORTANTE: Cierra el tema con firmeza y calidez en cuanto llames a la función,
                       por ejemplo: "¡Excelente! Continuemos."
                    4) Solo si el estudiante te pide explícitamente otro ejemplo, cuenta UNO del banco de ejemplos
                       de abajo.

                    TEXTO DE LA TARJETA QUE EL ESTUDIANTE TIENE EN PANTALLA (título: "{selected.Title}"):
                    {selectedText}

                    BANCO DE EJEMPLOS YA PREPARADOS:
                    {bank}

                    {deepDiveOpeningRule}

                    {sharedRules}
                    """;
        }

        if (stepType == ExperienceStepType.Recall)
        {
            var recallOpeningRule = isEnglish
                ? """
                    - Start talking YOURSELF immediately upon connecting, without waiting for the student
                      to say something first. NEVER open with a generic assistant greeting. Instead, start directly
                      with "Hi, let's now..." and enter immediately into reading the question below.
                    - That first turn is literally the FIRST of the conversation: the student has not yet said
                      a single word. Therefore, never use phrases implying they already answered.
                    """
                : """
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

            return isEnglish
                ? $"""
                    You are an AI Agent accompanying a student by voice in the Recall step (active recall).
                    Your only job is:
                    1) Read aloud, EXACTLY as written, the question below.
                    2) Listen attentively to the student's spoken answer.
                    3) When they finish answering, simply thank them and tell them to click "Submit" to send
                       their answer — YOU NEVER grade, never say if it's correct or incorrect, and never reveal
                       the correct answer.

                    QUESTION TO READ ALOUD:
                    {stepContent}

                    {recallOpeningRule}

                    {sharedRules}
                    """
                : $"""
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
