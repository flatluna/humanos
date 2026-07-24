using System.Net;
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

        ExperienceStepType stepType;
        NodeExperienceBlueprintStep? blueprintStep;
        string? currentPromptText = body.CurrentPromptText;

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
                && step.StepType != ExperienceStepType.Recall)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.BadRequest,
                    "VoiceNotYetSupportedForStep",
                    $"Voice Tutor is only available for Hypothesis, Teaching and Recall steps so far (this step is {step.StepType}).",
                    cancellationToken);
            }

            stepType = step.StepType;
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
            // Only Hypothesis/Teaching — Recall's voice flow always needs a
            // real, in-progress LearningSessionStep (it feeds the student's
            // transcribed answer back into the real grading path).
            if (parsedStepType != ExperienceStepType.Hypothesis && parsedStepType != ExperienceStepType.Teaching)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request,
                    HttpStatusCode.BadRequest,
                    "VoiceNotYetSupportedForStep",
                    $"Voice Tutor's blueprint-only path only supports Hypothesis and Teaching (got {body.StepType}).",
                    cancellationToken);
            }

            stepType = parsedStepType;
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

        var instructions = BuildInstructions(stepType, effectiveContent);

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
    /// Builds the Realtime session's system prompt entirely server-side,
    /// grounded in <paramref name="stepContent"/> (the SAME
    /// NodeExperienceBlueprintStep.Content TutorAgentV2 reads). Hard
    /// boundaries mirror TutorAgentV2's own (never grade, never invent
    /// content, never claim to unlock/advance anything) — this is a
    /// SEPARATE LLM session (Realtime), so it needs its own complete
    /// guardrail text rather than relying on TutorAgentV2's.
    /// </summary>
    private static string BuildInstructions(ExperienceStepType stepType, string stepContent)
    {
        const string hardBoundaries = """
            REGLAS OBLIGATORIAS (nunca las rompas):
            - Nunca inventes conocimiento fuera del texto proporcionado abajo.
            - Nunca calificas, evalúas ni decides si la respuesta del estudiante es correcta o incorrecta.
            - Nunca resuelves la tarea en lugar del estudiante ni le das la respuesta correcta.
            - Nunca afirmas poder desbloquear pasos ni cambiar el estado del curso — no tienes esa capacidad.
            - Habla siempre en español, con un tono cercano, breve y alentador.
            - Empieza a hablar TÚ MISMO de inmediato en cuanto se conecte, sin esperar a que el
              estudiante diga algo primero. NUNCA abras con un saludo genérico de asistente como
              "¡Hola! ¿En qué puedo ayudarte?" — en vez de eso, arranca directo con algo como "Hola,
              vamos ahora a..." y entra inmediatamente en tu tarea de abajo (leer la pregunta o
              explicar el contenido). El estudiante ya sabe en qué curso está; no se lo preguntes.
            - Ese primer turno es literalmente el PRIMERO de la conversación: el estudiante
              todavía no ha dicho ni una sola palabra. Por eso, en tu primer turno JAMÁS uses
              frases que den a entender que él ya respondió o dijo algo (como "perfecto",
              "genial", "entendido", "buena respuesta", "exacto") — eso confunde al estudiante,
              que sentirá que le respondes a algo que nunca dijo. Simplemente empieza tu tarea.
            """;

        if (stepType == ExperienceStepType.Hypothesis)
        {
            return $"""
                Eres un Agente de IA que acompaña por voz a un estudiante en el paso de Hipótesis.
                Tu único trabajo: lee en voz alta, exactamente como está escrita, la siguiente pregunta,
                y después escucha la respuesta hablada del estudiante. Cuando termine de responder,
                agradece su idea y anímalo a pulsar "Continuar" cuando esté listo — sin decir si es
                correcta o incorrecta, sin dar la respuesta correcta.

                PREGUNTA A LEER EN VOZ ALTA:
                {stepContent}

                {hardBoundaries}
                """;
        }

        if (stepType == ExperienceStepType.Recall)
        {
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

                {hardBoundaries}
                """;
        }

        // Teaching
        return $"""
            Eres un Agente de IA que acompaña por voz a un estudiante en el paso de Enseñanza,
            como un copiloto conversacional. Explica el contenido de abajo con tus propias palabras,
            de forma clara y en voz alta, y responde cualquier pregunta que el estudiante haga EN VIVO
            sobre este mismo contenido — nunca sobre otros temas.

            CONTENIDO DE ENSEÑANZA (tu única fuente de verdad):
            {stepContent}

            {hardBoundaries}
            """;
    }
}
