using System.Net;
using System.Text;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Models.Learning;
using HumanOS.Models.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// "Reactivar Engrama" — mints a short-lived Azure OpenAI GPT Realtime
/// ephemeral session for a full-lesson ACTIVE RECALL review conversation on
/// an already-Mastered node (shown as a second action alongside "Practicar
/// de nuevo" on the node-summary recap screen, product decision
/// 2026-07-24). This is deliberately NOT an exam/quiz and NEVER grades
/// anything — it's a study-companion conversation where the Agent walks
/// the student back through everything they learned in this node
/// (Hypothesis → Teaching → Recall → Production → Assessment) and asks
/// them to recall it in their own words, offering hints if they get stuck,
/// same spirit as reviewing material before a test that never actually
/// happens.
///
/// Grounded ENTIRELY in the student's own most recent completed attempt —
/// reuses <see cref="InstructorRuntimeOrchestrator.GetNodeSummaryAsync"/>
/// (the exact same read-only data source as the node-summary recap screen)
/// so the Agent can also reference the student's own past written answers,
/// not just the blueprint's static content. Never mutates anything (no
/// LearningSession/LearningSessionStep/LearningEvidence writes) — purely a
/// parallel, ephemeral voice channel, same architecture as
/// VoiceTutorSessionFunction.
/// </summary>
public sealed class EngramReviewSessionFunction
{
    private readonly RealtimeVoiceSessionService _voiceSessionService;
    private readonly InstructorRuntimeOrchestrator _orchestrator;
    private readonly HumanOsDbContext _dbContext;
    private readonly NodeKnowledgeIndexService _knowledgeIndexService;
    private readonly ILearnerContextBuilder _learnerContextBuilder;

    public EngramReviewSessionFunction(
        RealtimeVoiceSessionService voiceSessionService,
        InstructorRuntimeOrchestrator orchestrator,
        HumanOsDbContext dbContext,
        NodeKnowledgeIndexService knowledgeIndexService,
        ILearnerContextBuilder learnerContextBuilder)
    {
        _voiceSessionService = voiceSessionService;
        _orchestrator = orchestrator;
        _dbContext = dbContext;
        _knowledgeIndexService = knowledgeIndexService;
        _learnerContextBuilder = learnerContextBuilder;
    }

    [Function("EngramReviewSession")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "instructor-runtime/tutor/engram-review-session")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_voiceSessionService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "EngramReviewNotConfigured",
                "Reactivar Engrama is not configured (missing AzureOpenAIRealtimeDeploymentName/AzureOpenAIApiKey settings).",
                cancellationToken);
        }

        EngramReviewSessionRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<EngramReviewSessionRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.PersonId == Guid.Empty || body.CapabilityGraphNodeId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "PersonId and CapabilityGraphNodeId are both required.", cancellationToken);
        }

        InstructorRuntimeOrchestrator.NodeSummaryResult summary;
        try
        {
            summary = await _orchestrator.GetNodeSummaryAsync(_dbContext, body.PersonId, body.CapabilityGraphNodeId, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NodeNotCompletedYet", ex.Message, cancellationToken);
        }

        // Extra personalization context — deliberately queried directly here
        // (rather than added to the shared NodeSummaryResult, which also
        // feeds the node-summary recap screen) so this feature's needs
        // don't leak into that unrelated read model.
        var node = await _dbContext.CapabilityGraphNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.CapabilityGraphNodeId == body.CapabilityGraphNodeId, cancellationToken);

        var profile = await _dbContext.PersonProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PersonId == body.PersonId, cancellationToken);

        var latestAssessment = await _dbContext.LearningAssessmentResults
            .AsNoTracking()
            .Where(a => a.LearningSessionNodeId == summary.LearningSessionNodeId && a.Passed)
            .OrderByDescending(a => a.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken);

        var latestAttempt = summary.PastAttempts.FirstOrDefault(a => a.LearningSessionNodeId == summary.LearningSessionNodeId);

        // Illustrations already generated for this node's steps (2026-07-24)
        // -- deliberately reused AS-IS, never generated on demand: on-demand
        // image generation (e.g. gpt-image-1.5) would add several seconds of
        // latency mid-conversation, which doesn't fit a live voice call. The
        // model instead picks among these EXISTING images (or, as a lighter
        // OPTIONAL fallback, an emoji-based example or a simple bar chart it
        // builds itself, both instant/free) via a single Realtime "tool"
        // call whenever a story/example it's telling would genuinely
        // benefit from a visual -- see BuildInstructions/BuildVisualAidTools.
        var illustrations = summary.Steps
            .SelectMany(s => s.Illustrations)
            .GroupBy(i => i.CapabilityGraphNodeIllustrationId)
            .Select(g => g.First())
            .ToList();

        // Optional per-card focus (2026-08-02) — lets the Brain page's card
        // browser ask the Agent to explain one specific Deep Dive
        // ExampleCard instead of the whole lesson. Silently falls back to
        // the original whole-lesson review when the index is missing/out
        // of range, so this stays fully backward compatible.
        ExampleCard? focusCard = null;
        if (body.ExampleCardIndex is int cardIndex)
        {
            var deepDiveStep = summary.Steps.FirstOrDefault(s => s.StepType == ExperienceStepType.DeepDive);
            if (deepDiveStep?.ExampleCards is { } exampleCards && cardIndex >= 0 && cardIndex < exampleCards.Count)
            {
                focusCard = exampleCards[cardIndex];
            }
        }

        // Course-wide chapter awareness (2026-08-06) -- see search_other_chapters'
        // doc comment on BuildKnowledgeSearchTool for the full rationale: a lightweight
        // list of sibling chapter NAMES (not their content) is cheap enough to always
        // include, so the Agent knows other chapters exist even when it can't answer
        // from them directly -- the actual content is fetched on demand via the tool.
        var otherChapterNames = node is null
            ? []
            : await _dbContext.CapabilityGraphNodes
                .AsNoTracking()
                .Where(n => n.CapabilityGraphId == node.CapabilityGraphId && n.CapabilityGraphNodeId != body.CapabilityGraphNodeId)
                .OrderBy(n => n.SortOrder)
                .Select(n => n.Name)
                .ToListAsync(cancellationToken);
        var knowledgeSearchAvailable = _knowledgeIndexService.IsConfigured && otherChapterNames.Count > 0;

        var instructions = BuildInstructions(summary, node, profile, latestAssessment, latestAttempt, illustrations, focusCard, otherChapterNames, knowledgeSearchAvailable);

        // Personalized LEARNER CONTEXT injection
        var courseId = node is not null
            ? await _dbContext.CapabilityGraphs
                .AsNoTracking()
                .Where(g => g.CapabilityGraphId == node.CapabilityGraphId)
                .Select(g => (Guid?)g.CapabilityId)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var learnerContext = await _learnerContextBuilder.BuildLearnerContextBlockAsync(
            body.PersonId, courseId, body.CapabilityGraphNodeId.ToString(), cancellationToken);

        if (!string.IsNullOrWhiteSpace(learnerContext))
        {
            instructions = $"{learnerContext}\n\n{instructions}";
        }

        var tools = knowledgeSearchAvailable
            ? new object[] { BuildShowVisualAidTool(illustrations), BuildKnowledgeSearchTool(), BuildCompleteReviewTool() }
            : new object[] { BuildShowVisualAidTool(illustrations), BuildCompleteReviewTool() };

        try
        {
            var session = await _voiceSessionService.CreateEphemeralSessionAsync(instructions, cancellationToken, tools);

            return await FunctionResponseFactory.SuccessResponseAsync(request, new VoiceTutorSessionResponseDto
            {
                ClientSecret = session.ClientSecret,
                RealtimeCallsUrl = session.RealtimeCallsUrl,
                Model = session.Model,
                Voice = session.Voice,
                ExpiresAtUnixSeconds = session.ExpiresAtUnixSeconds,
                Illustrations = illustrations
                    .Select(i => new IllustrationRefDto { Id = i.CapabilityGraphNodeIllustrationId, Caption = i.Caption })
                    .ToList()
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadGateway, "VoiceSessionMintFailed", ex.Message, cancellationToken);
        }
    }

    /// <summary>
    /// Builds the single Realtime "tool" (function-calling) definition that
    /// lets the model show a visual aid mid-conversation, in one of FOUR
    /// forms -- all zero-latency (never generates a new image on demand):
    /// - "illustration": one of this node's ALREADY-GENERATED illustrations
    ///   (id from the catalog in the prompt). Rendered via the existing
    ///   GetIllustrationImageFunction endpoint -- only an id crosses the
    ///   tool call, never image bytes.
    /// - "emoji": a quick emoji-based example the model composes itself
    ///   (e.g. a few pizza emojis + a short caption) -- purely optional and
    ///   playful, for when no real illustration fits but a tiny visual
    ///   still helps.
    /// - "chart": a simple bar-style comparison of labeled values the model
    ///   composes itself (e.g. comparing quantities/percentages) -- rendered
    ///   client-side as plain CSS bars, no charting library/image involved.
    /// - "text": free-form written text the model composes itself (e.g.
    ///   spelling out a word, writing a key phrase/definition/formula/
    ///   equation/code snippet) -- rendered like a small whiteboard
    ///   ("pizarrón") on screen. Works for ANY subject, not just language
    ///   lessons (e.g. "muéstrame cómo es una ecuación de segundo grado").
    ///   Added 2026-07-24 after live testing showed the student asking the
    ///   Agent to WRITE something (e.g. "how do you spell good morning")
    ///   had no way to actually display it.
    /// The frontend (EngramReviewAgent.tsx) listens for the corresponding
    /// response.output_item.done/response.done event with
    /// type == "function_call" and renders whichever kind was sent.
    /// </summary>
    private static object BuildShowVisualAidTool(List<InstructorRuntimeOrchestrator.IllustrationRef> illustrations)
    {
        var illustrationIds = illustrations.Select(i => i.CapabilityGraphNodeIllustrationId.ToString()).ToArray();

        return new
            {
                type = "function",
                name = "show_visual_aid",
                description = "Muestra en pantalla una ayuda visual u OPCIONAL justo cuando estes contando una " +
                    "historia, dando un ejemplo de la vida real, cuando el estudiante te pida explicitamente " +
                    "que ESCRIBAS algo (como se escribe una palabra, una frase, una ecuacion, una formula, un " +
                    "fragmento de codigo, etc. -- de CUALQUIER materia, no solo idiomas), o cuando te pida " +
                    "explicitamente una IMAGEN/DIBUJO/FOTO de una escena o cosa concreta que no sea ya una de " +
                    "las ilustraciones del catalogo -- en esos casos SIEMPRE usa kind=\"text\" o " +
                    "kind=\"generated_image\" segun corresponda para realmente mostrarlo en pantalla, nunca te " +
                    "limites a decirlo solo en voz. Usalo con moderacion para los demas casos (no en cada turno, " +
                    "quiza 1-3 veces en toda la conversacion) -- solo cuando de verdad aporte valor.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        kind = new
                        {
                            type = "string",
                            @enum = new[] { "illustration", "emoji", "chart", "text", "generated_image" },
                            description = "illustration para usar una imagen real ya existente del catalogo; " +
                                "emoji para un ejemplo rapido con uno o varios emojis; chart para una " +
                                "comparacion simple de valores en barras; text para ESCRIBIR texto libre en " +
                                "pantalla como en un pizarron -- una palabra, una ecuacion, una formula, codigo, " +
                                "etc., de cualquier materia (usa esta SIEMPRE que el estudiante pida que le " +
                                "escribas, deletrees o muestres por escrito algo); generated_image para DIBUJAR " +
                                "una imagen nueva bajo demanda de una escena o cosa concreta que el estudiante " +
                                "pidio ver y que NO esta ya en el catalogo de ilustraciones (usa esta SIEMPRE " +
                                "que el estudiante pida explicitamente ver una imagen/foto/dibujo de algo que no " +
                                "sea una de las ilustraciones existentes)."
                        },
                        illustration_id = new
                        {
                            type = "string",
                            @enum = illustrationIds,
                            description = "Solo si kind=illustration: el id exacto tomado del catalogo del prompt."
                        },
                        emoji = new
                        {
                            type = "string",
                            description = "Solo si kind=emoji: uno o varios emojis que ilustren el ejemplo."
                        },
                        text = new
                        {
                            type = "string",
                            description = "Solo si kind=text: el texto EXACTO a escribir en el pizarron -- una " +
                                "palabra, una frase, como se deletrea algo, una ecuacion, una formula matematica, " +
                                "un fragmento de codigo, etc. (de cualquier materia, no solo idiomas). TAMBIEN " +
                                "puedes usar VARIAS LINEAS (separadas con salto de linea) combinando emojis " +
                                "como iconos y flechas de texto (->, =>, o el caracter real \u2192) para dibujar " +
                                "diagramas de flujo simples, listas de pasos, comparaciones lado a lado, o " +
                                "mapas mentales sencillos -- por ejemplo: \"\ud83c\udf05 Despertar -> \ud83c\udf73 Desayunar -> \ud83d\ude97 Ir al trabajo\". " +
                                "No uses HTML/markdown ni caracteres de caja (\u2502\u2500\u250c) -- solo texto plano, " +
                                "emojis y flechas simples, ya que se muestra tal cual, sin renderizar HTML."
                        },
                        image_prompt = new
                        {
                            type = "string",
                            description = "Solo si kind=generated_image: una descripcion clara y detallada (en " +
                                "ingles funciona mejor para el generador de imagenes, pero puedes escribirla en " +
                                "español si prefieres) de la escena exacta que el estudiante pidio ver -- por " +
                                "ejemplo 'two friends waving and greeting each other good morning outdoors, " +
                                "simple friendly cartoon illustration'. Se especifico y describe la escena " +
                                "completa, no solo un objeto suelto."
                        },
                        caption = new
                        {
                            type = "string",
                            description = "Texto breve que acompana al emoji, al chart, al texto del pizarron, o a la imagen generada."
                        },
                        chart_items = new
                        {
                            type = "array",
                            description = "Solo si kind=chart: lista de 2 a 5 valores a comparar en barras.",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    label = new { type = "string" },
                                    value = new { type = "number" }
                                },
                                required = new[] { "label", "value" }
                            }
                        }
                    },
                    required = new[] { "kind" }
                }
            };
    }

    /// <summary>
    /// The Realtime tool that lets the Agent fetch content from OTHER
    /// chapters/nodes of the SAME course, on demand, whenever the student
    /// asks about something not covered in the current lesson's own
    /// material (e.g. reviewing "Control de edición" but asking about
    /// "canal de denuncia", a different node). Deliberately NOT solved by
    /// stuffing every other chapter's full content into the system prompt
    /// -- for a course with many chapters that would be enormous (and
    /// costly/latent) on EVERY session, most of it irrelevant to any given
    /// conversation. Instead this reuses the same embedded-chunk vector
    /// search already built for TutorService's text-based cross-node RAG
    /// (see NodeKnowledgeIndexService/SearchNodeKnowledgeFunction) --
    /// cheap (one embedding call + a vector search per actual question) and
    /// scales to any course size. Only offered when the index has other
    /// chapters to search (see knowledgeSearchAvailable in RunAsync).
    /// </summary>
    private static object BuildKnowledgeSearchTool() => new
    {
        type = "function",
        name = "search_other_chapters",
        description = "Busca contenido de OTROS capitulos/lecciones de este mismo curso (no de esta " +
            "leccion actual) cuando el estudiante pregunte por un tema, dato o nombre que NO esta " +
            "cubierto en el material de esta leccion. El curso tiene muchos otros capitulos con " +
            "contenido distinto (ver la lista en tus instrucciones) -- esta es la UNICA forma de " +
            "consultarlos. Usala SIEMPRE que la pregunta del estudiante parezca ser sobre otro tema " +
            "del curso en lugar de esta leccion -- nunca inventes ni asumas que sabes que dice otro " +
            "capitulo sin buscarlo primero aqui.",
        parameters = new
        {
            type = "object",
            properties = new
            {
                query = new
                {
                    type = "string",
                    description = "La pregunta o tema exacto que menciono el estudiante, reformulado brevemente si hace falta para buscarlo."
                }
            },
            required = new[] { "query" }
        }
    };

    /// <summary>
    /// The Realtime tool that lets the Agent signal (product decision
    /// 2026-08-07) that it just walked the student through the ENTIRE
    /// lesson from beginning to end, with genuine active-recall engagement
    /// (not a session the student barely started or cut short). The
    /// frontend answers this tool call AND, separately, calls the
    /// EngramReviewComplete endpoint to persist an
    /// <see cref="HumanOS.Models.Learning.EngramReviewCompletion"/> row --
    /// this refreshes the Brain page's forgetting-curve memory signal back
    /// to full strength, exactly like a graded practice attempt would. Has
    /// no parameters -- it is purely a one-time signal the model decides to
    /// send at most ONCE per conversation, never mid-lesson.
    /// </summary>
    private static object BuildCompleteReviewTool() => new
    {
        type = "function",
        name = "complete_review",
        description = "Llama a esta funcion UNA SOLA VEZ, y solo cuando genuinamente hayas repasado " +
            "con el estudiante TODO el contenido esencial de la leccion completa de principio a fin " +
            "(no solo una parte), con verdadero recuerdo activo de su parte (el estudiante participo, " +
            "respondio, o confirmo que recordaba las ideas -- no una sesion que apenas empezo o que se " +
            "corto a la mitad). NUNCA la llames si la conversacion fue breve, parcial, o si el " +
            "estudiante se fue/cerro la sesion antes de terminar el repaso completo. Llamala justo " +
            "despues de tu resumen final de cierre, no antes.",
        parameters = new { type = "object", properties = new { } }
    };


    /// <summary>
    /// Builds the Realtime session's system prompt entirely server-side,
    /// grounded in the student's OWN most recent completed attempt at this
    /// node (blueprint Content for all 5 steps, plus their own past written
    /// answers where present), plus light personalization context (student
    /// name, lesson name/number, score, time spent, past feedback). Hard
    /// boundaries mirror VoiceTutorSessionFunction's (never grade, never
    /// invent content, never claim to unlock/advance anything) — this is a
    /// SEPARATE Agent persona ("Reactivar Engrama"), explicitly NOT an
    /// exam: no scoring, no pass/fail, no simulated question-and-answer
    /// test. The review treats the lesson as ONE whole (not a rigid
    /// step-by-step march starting at Hypothesis) since Hypothesis is just
    /// the student's initial guess/motivator, not real lesson content —
    /// dwelling on it first was making the agent get stuck there.
    /// </summary>
    private static string BuildInstructions(
        InstructorRuntimeOrchestrator.NodeSummaryResult summary,
        CapabilityGraphNode? node,
        PersonProfile? profile,
        LearningAssessmentResult? latestAssessment,
        InstructorRuntimeOrchestrator.NodeAttemptSummary? latestAttempt,
        List<InstructorRuntimeOrchestrator.IllustrationRef> illustrations,
        ExampleCard? focusCard = null,
        List<string>? otherChapterNames = null,
        bool knowledgeSearchAvailable = false)
    {
        var lesson = new StringBuilder();
        foreach (var step in summary.Steps)
        {
            lesson.AppendLine($"--- {step.StepType} ---");
            lesson.AppendLine(StripHtml(step.StepContent));
            if (step.Evidence.Count > 0)
            {
                lesson.AppendLine("Lo que el estudiante escribió/respondió aquí la última vez:");
                foreach (var evidence in step.Evidence)
                {
                    lesson.AppendLine($"- \"{evidence.StudentResponse}\"");
                }
            }
            lesson.AppendLine();
        }

        var studentName = !string.IsNullOrWhiteSpace(profile?.DisplayName)
            ? profile!.DisplayName!
            : !string.IsNullOrWhiteSpace(profile?.FirstName)
                ? $"{profile!.FirstName} {profile.LastName}".Trim()
                : "el estudiante";

        var lessonName = !string.IsNullOrWhiteSpace(node?.Name) ? node!.Name : "esta lección";
        var lessonNumber = node is not null ? node.SortOrder.ToString() : "—";
        var score = latestAssessment?.Score is int s ? $"{s}/100" : "—";
        var weaknesses = !string.IsNullOrWhiteSpace(latestAssessment?.Feedback)
            ? StripHtml(latestAssessment!.Feedback!)
            : "No hay retroalimentación previa registrada.";
        var timeSpent = FormatDuration(latestAttempt?.StartedDate, latestAttempt?.CompletedDate);

        var illustrationCatalog = illustrations.Count == 0
            ? "(Esta lección no tiene ilustraciones disponibles -- nunca uses kind=\"illustration\", pero puedes usar kind=\"emoji\" o kind=\"chart\" si aportan valor.)"
            : string.Join(
                "\n",
                illustrations.Select(i => $"- id \"{i.CapabilityGraphNodeIllustrationId}\": {(string.IsNullOrWhiteSpace(i.Caption) ? "(sin descripcion)" : i.Caption)}"));

        // Per-card focus (2026-08-02) — when the student picked a specific
        // Deep Dive ExampleCard from the Brain page's card browser instead
        // of asking for a whole-lesson review, steer the conversation to
        // anchor on THAT card while the full lesson material further below
        // still stays available as background context (so the agent can
        // still connect it to the rest of the lesson if organically
        // relevant, it just shouldn't march through every other card).
        var cardFocus = focusCard is null
            ? string.Empty
            : $"""

                FOCO DE ESTA CONVERSACIÓN: el estudiante eligió repasar específicamente esta tarjeta:
                - Título: "{focusCard.Title}"
                - Contenido: {StripHtml(focusCard.Text)}
                {(string.IsNullOrWhiteSpace(focusCard.Emoji) ? "" : $"- Emoji asociado: {focusCard.Emoji}")}
                Concéntrate en explicar y contar historias/ejemplos sobre ESTA tarjeta específicamente —
                no marches por el resto de la lección de forma genérica. Puedes conectar brevemente con
                el resto del material de abajo si surge de forma natural, pero el ancla de la
                conversación es esta tarjeta.
                """;

        // These two guidance strings previously hardcoded a "review the whole
        // lesson in general" instruction unconditionally, which conflicted
        // with cardFocus above and made the agent ignore the chosen card
        // (2026-08-02 bug: picking a card and asking to be explained it sent
        // the student back into a whole-lesson review instead). Now the
        // opening turn and the overall scope both explicitly anchor on
        // focusCard when one was chosen.
        var scopeGuidance = focusCard is null
            ? """
                No sigas una estructura rígida de 5 pasos ni le pidas explícitamente que recuerde la
                "Hipótesis" -- trata todo el material de abajo como una sola lección a repasar,
                priorizando las partes donde su retroalimentación previa indica que le costó más.
                """
            : $"""
                El estudiante ya eligió repasar específicamente la tarjeta "{focusCard.Title}" (ver FOCO
                DE ESTA CONVERSACIÓN más abajo) -- toda la conversación debe girar en torno a ESA
                tarjeta, no marches por el resto de la lección ni la trates como un repaso general.
                """;

        var openingGuidance = focusCard is null
            ? """
                salúdalo por su nombre, menciona el nombre de la lección, y arranca
                directo preguntándole qué recuerda de la lección en general (no le pidas
                específicamente que recuerde la "Hipótesis" — ese fue solo el punto de partida
                motivacional, no contenido real; enfócate en las ideas y habilidades enseñadas).
                """
            : $"""
                salúdalo por su nombre y menciona que hoy van a repasar juntos la tarjeta
                "{focusCard.Title}" en particular. Arranca directo contándole un breve ejemplo o
                historia relacionado con ESA tarjeta específica, o preguntándole qué recuerda sobre
                ella -- NUNCA le preguntes por la lección en general ni menciones otras tarjetas.
                """;

        // Course-wide chapter awareness (2026-08-06) -- see BuildKnowledgeSearchTool's
        // doc comment for the full rationale: only chapter NAMES are listed here (cheap),
        // never their content -- the real content is fetched on demand via the
        // search_other_chapters tool, only for the topics the student actually asks about.
        var otherChaptersBlock = !knowledgeSearchAvailable || otherChapterNames is not { Count: > 0 }
            ? string.Empty
            : $"""

                OTROS CAPÍTULOS DE ESTE CURSO (solo para que sepas que existen -- NO tienes su contenido
                aquí, solo sus nombres):
                {string.Join("\n", otherChapterNames.Select(n => $"- {n}"))}
                Si el estudiante pregunta algo que suene a uno de estos temas (o cualquier cosa que no
                esté en el MATERIAL COMPLETO DE LA LECCIÓN de abajo), usa la función
                "search_other_chapters" para buscar el contenido real antes de responder -- nunca
                inventes ni asumas qué dice otro capítulo.
                """;

        var studentContext = $"""
            CONTEXTO DEL ESTUDIANTE (información de fondo para TI, el agente — úsala para personalizar
            el tono y saber en qué apoyarte más, pero NO la recites como si fuera una lista o un
            reporte, y NUNCA menciones el puntaje/nota como una calificación de esta conversación):
            - Nombre del estudiante: {studentName}
            - Lección: "{lessonName}" (lección número {lessonNumber} de su programa)
            - Puntaje que obtuvo la última vez que completó la evaluación de esta lección: {score}
            - Tiempo que le tomó completar la lección la última vez: {timeSpent}
            - Debilidades/retroalimentación registrada de su última evaluación (usa esto para saber
              qué partes conviene reforzar más durante el repaso, con más pistas o repitiendo esas
              ideas con otras palabras): {weaknesses}
            """;

        var hardBoundaries = $"""
            REGLAS OBLIGATORIAS (nunca las rompas):
            - Esto NO es un examen ni un cuestionario de preguntas y respuestas — nunca digas que
              el estudiante "aprobó", "reprobó", ni le des una puntuación o nota de ningún tipo.
            - Nunca inventes conocimiento fuera del material de la lección proporcionado abajo.
            - Nunca calificas ni evalúas si lo que el estudiante recuerda es correcto o incorrecto
              — si se equivoca o le falta algo, simplemente ayúdalo a reconstruirlo con pistas
              suaves y recordándole partes del material, nunca lo corriges como un profesor que
              califica un examen.
            - Nunca afirmas poder cambiar su progreso, desbloquear pasos ni modificar su registro
              — esta conversación es solo un repaso mental, no toca su historial real.
            - Habla siempre en español, con un tono cálido, cercano y alentador — como un amigo
              que te ayuda a repasar antes de dormir, no como un evaluador.
            - Empieza a hablar TÚ MISMO de inmediato en cuanto se conecte, sin esperar a que el
              estudiante diga algo primero. NUNCA abras con un saludo genérico de asistente. En
              vez de eso, {openingGuidance}
            - Ese primer turno es el PRIMERO de la conversación: el estudiante todavía no ha dicho
              nada. Por eso, en tu primer turno JAMÁS uses frases que den a entender que él ya
              respondió algo.
            - Al llamar a la función show_visual_aid con kind="illustration", usa EXACTAMENTE uno de
              los ids del catálogo de abajo -- nunca inventes un id, y nunca uses ese kind si el
              catálogo dice que no hay ilustraciones disponibles (en ese caso puedes igual usar
              kind="emoji" o kind="chart" si aportan valor).
            - Si el estudiante te pide explícitamente que escribas, deletrees o muestres algo por
              escrito -- una palabra, una ecuación, una fórmula, código, etc., de CUALQUIER materia --
              SIEMPRE llama a show_visual_aid con kind="text" y el texto exacto pedido -- nunca
              respondas solo en voz a ese tipo de pedido.
            - Si el estudiante te pide explícitamente VER una imagen/foto/dibujo de una escena o cosa
              concreta que NO está ya en el catálogo de ilustraciones de abajo, SIEMPRE llama a
              show_visual_aid con kind="generated_image" y un image_prompt claro y detallado de esa
              escena exacta -- nunca digas que no puedes mostrar imágenes ni respondas solo en voz a
              ese tipo de pedido. Generar la imagen toma unos segundos: puedes decir algo breve como
              "dame un segundo, te lo dibujo" mientras se genera.
            {(knowledgeSearchAvailable ? "- Si el estudiante pregunta algo que NO está en el MATERIAL COMPLETO DE LA LECCIÓN de abajo (por ejemplo, algo de otro capítulo del curso), SIEMPRE llama primero a search_other_chapters con su pregunta -- nunca inventes ni asumas contenido de otro capítulo sin buscarlo, y nunca digas simplemente que \"no tienes esa información\" sin haber buscado antes." : "")}
            - Cuando -- y SOLO cuando -- de verdad hayas repasado con él TODO el contenido esencial de
              la lección completa de principio a fin, con recuerdo activo genuino de su parte, llama a
              la función complete_review justo después de tu resumen final de cierre (una sola vez,
              nunca a mitad de la conversación). Si la conversación fue corta, parcial, o el estudiante
              se despide antes de terminar el repaso completo, NO la llames.
            """;

        return $"""
            Eres el "Agente Engrama" -- un compañero de estudio por voz que ayuda a un estudiante a
            REACTIVAR de memoria una lección que YA DOMINÓ, mediante recuerdo activo conversacional
            (active recall), NO mediante un examen. NO eres un cuestionario rígido de preguntas y
            respuestas: eres más como un mentor conversando cerca de una fogata. Puedes -- y debes --
            contar pequeñas HISTORIAS sobre cómo se usa lo que aprendió en la vida real, dar
            EJEMPLOS concretos y cotidianos, hacer analogías, y solo de vez en cuando preguntarle
            directamente "¿qué recuerdas de...?" o "¿cómo explicarías...?" -- varía el ritmo, no lo
            conviertas en una entrevista. Cuando él responda o le falte algo, ayúdalo a reconstruirlo
            con pistas e historias que disparen su memoria -- nunca resolviéndoselo tú de entrada de
            forma seca, nunca calificándolo. {scopeGuidance} Cuando sientas que ya cubrieron lo
            esencial, cierra con un resumen breve y alentador de todo lo que logró recordar, sin dar
            ninguna nota ni puntaje.

            AYUDAS VISUALES (OPCIONAL): cuando estés contando una historia o dando un ejemplo de la
            vida real, puedes llamar a la función show_visual_aid para mostrar algo en pantalla justo
            en ese momento -- esto hace la historia más vívida. Tienes cinco formas, elige la que
            mejor encaje:
            - kind="illustration": una imagen real ya existente de esta lección (usa su id exacto del
              catálogo de abajo).
            - kind="emoji": uno o varios emojis que ilustren tu ejemplo (ej. tres emojis de pizza para
              "3 porciones"), con un caption breve.
            - kind="chart": una comparación simple de 2 a 5 valores en barras (label + value), útil
              para ejemplos con cantidades o porcentajes.
            - kind="text": ESCRIBE texto libre en pantalla, como en un pequeño pizarrón -- una palabra,
              una ecuación, una fórmula, código, etc., de CUALQUIER materia (no solo idiomas). Úsala
              SIEMPRE que el estudiante te pida explícitamente que escribas, deletrees o muestres por
              escrito algo. TAMBIÉN puedes usar varias líneas con emojis como iconos y flechas de
              texto (-> o →) para dibujar diagramas de flujo simples, listas de pasos o comparaciones
              lado a lado -- es solo texto plano (nunca HTML), pero combinado con emojis y flechas es
              muy efectivo para ilustrar procesos o secuencias. En ese caso nunca te limites a decirlo
              solo en voz -- muéstralo también con esta función.
            - kind="generated_image": DIBUJA una imagen nueva bajo demanda (image_prompt claro y
              detallado) de una escena o cosa concreta que el estudiante pidió ver y que NO está ya en
              el catálogo de ilustraciones. Úsala SIEMPRE que el estudiante pida explícitamente ver una
              imagen/foto/dibujo de algo así -- nunca digas que no puedes mostrar imágenes.
            Para emoji/chart/illustration, úsalo con moderación (nunca en cada turno, quizás 1-3 veces
            en toda la conversación), solo cuando de verdad aporte valor visual -- es un extra
            opcional, no una obligación en cada historia. kind="text" y kind="generated_image" en
            cambio úsalas cada vez que el estudiante lo pida explícitamente.

            CATÁLOGO DE ILUSTRACIONES DISPONIBLES PARA ESTA LECCIÓN (para kind="illustration"):
            {illustrationCatalog}
            {cardFocus}
            {studentContext}
            {otherChaptersBlock}

            MATERIAL COMPLETO DE LA LECCIÓN (tu única fuente de verdad -- no inventes nada fuera de
            esto):

            {lesson}

            {hardBoundaries}
            """;
    }

    private static string FormatDuration(DateTime? started, DateTime? completed)
    {
        if (started is null || completed is null || completed < started)
        {
            return "—";
        }

        var span = completed.Value - started.Value;
        if (span.TotalMinutes < 1)
        {
            return "menos de 1 minuto";
        }
        if (span.TotalHours < 1)
        {
            return $"{(int)span.TotalMinutes} minutos";
        }
        return $"{(int)span.TotalHours}h {span.Minutes}min";
    }

    private static string StripHtml(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html ?? string.Empty, "<[^>]+>", " ").Trim();
}
