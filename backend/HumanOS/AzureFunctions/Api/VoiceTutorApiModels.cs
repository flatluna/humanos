namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Request/response DTOs for the voice Tutor's ephemeral-session endpoint
/// (see VoiceTutorSessionFunction.cs and Services/RealtimeVoiceSessionService.cs).
/// Kept in their own file, matching this codebase's one-*ApiModels.cs-per-
/// feature-area convention (see TutorApiModels.cs).
/// </summary>
public sealed class VoiceTutorSessionRequest
{
    /// <summary>Optional. Authenticated or explicitly passed employee/person id
    /// used to build personalized LEARNER CONTEXT for the Realtime voice tutor.</summary>
    public Guid? PersonId { get; set; }

    /// <summary>Live-session path: an existing LearningSessionStep the
    /// student is actually on (or has already completed). Leave
    /// Guid.Empty when using the blueprint-only path below instead.</summary>
    public Guid LearningSessionStepId { get; set; }

    /// <summary>Blueprint-only path (2026-07-22) — Capability Studio's demo
    /// "read-only peek" lets a reviewer jump to ANY of the 5 steps with no
    /// LearningSession progression gating, so a peeked step (e.g. Teaching,
    /// peeked before the live session has actually reached it) may have NO
    /// LearningSessionStep row yet. When LearningSessionStepId is empty,
    /// both CapabilityGraphNodeId and StepType must be set instead — the
    /// voice Agent then reads straight from the node's most recent
    /// NodeExperienceBlueprintStep.Content, with no session context at all
    /// (never grades, same as the live path). Only Hypothesis/Teaching are
    /// supported here (mirrors this endpoint's overall step-type scope).</summary>
    public Guid? CapabilityGraphNodeId { get; set; }

    /// <summary>One of "Hypothesis"/"Teaching" — required alongside <see cref="CapabilityGraphNodeId"/> when not using LearningSessionStepId.</summary>
    public string? StepType { get; set; }

    /// <summary>Optional. For Recall steps, the prompt currently rotates
    /// per attempt (TutorAgentV2/RecallLoopGate), so the caller passes the
    /// EXACT text presently shown on screen here. Ignored for other step
    /// types (which read the blueprint's fixed Content instead). Never
    /// used for anything other than telling the voice Agent what question
    /// to read aloud — it grants no grading authority.</summary>
    public string? CurrentPromptText { get; set; }

    /// <summary>Optional (2026-07-25). The student's display name, used
    /// ONLY for Hypothesis/Teaching's opening greeting ("Bienvenido,
    /// {PersonName}. ¿Listo para comenzar?"). Never used for anything
    /// else. Null/blank falls back to a nameless "Bienvenido."</summary>
    public string? PersonName { get; set; }

    /// <summary>Required when StepType (or the live step being talked
    /// about) is Examples (2026-07-29) — 0-based index into that step's
    /// ExampleCards list identifying WHICH card's "habla con tu maestro"
    /// button the student pressed. The voice Agent explains that specific
    /// card (with its resolved result) and, if the student asks for
    /// another example, draws on the step's OTHER already-prepared cards
    /// before improvising a new, closely-analogous one. Ignored for every
    /// other step type.</summary>
    public int? ExampleCardIndex { get; set; }

    /// <summary>DeepDive blueprint-only path only (2026-07-30) — set when
    /// the student is revisiting an ALREADY-MASTERED card via the read-only
    /// card index (Capability Studio's PreviewNodePage), NOT actively
    /// working through it. Switches the DeepDive instructions from the
    /// normal "explain, then quiz the student back, then call
    /// mark_explanation_understood" loop to a plain, no-tools explain +
    /// invite-questions conversation — explicitly NOT a test, since the
    /// student already mastered this card. Ignored for every other
    /// StepType/path.</summary>
    public bool IsReviewOnly { get; set; }

    /// <summary>Traditional Course path (2026-08-09) — a single
    /// <c>CapabilityLesson</c> (a "subchapter", NOT its whole parent
    /// chapter — matches Graph mode's per-node granularity). Provide this
    /// ALONE, instead of LearningSessionStepId/CapabilityGraphNodeId —
    /// takes priority over both if somehow given together. The voice
    /// Agent reads this ONE lesson's cards and holds a free Q&amp;A/
    /// study-help conversation grounded in it; never grades (the parent
    /// chapter's final exam is a separate, deterministic multiple-choice
    /// flow — see ChapterExamAgent/gradeChapterExam).</summary>
    public Guid? TraditionalCourseLessonId { get; set; }

    /// <summary>Structured Learning path (2026-08-10) — a single
    /// <c>StructuredLearningSectionStep</c> (Hypothesis/Teaching only,
    /// same scope rule as every other path). Provide this ALONE, instead
    /// of every other target field — takes priority over all of them if
    /// somehow given together. The voice Agent reads that step's own
    /// Content aloud and holds a free study-help conversation grounded in
    /// it; never grades (Recall/Assessment for Structured Learning stay
    /// text-only, same rationale as Graph's own Recall/Assessment voice
    /// exclusion).</summary>
    public Guid? StructuredLearningSectionStepId { get; set; }

    /// <summary>Structured Learning Q&amp;A path only (2026-08-18) — when
    /// true alongside <see cref="StructuredLearningSectionStepId"/>, the
    /// voice Agent greets ONCE, then waits silently for the student's
    /// spoken question instead of teaching the whole section content
    /// end-to-end. Used by AI Agents' course chat page, where the student
    /// picks either voice OR text to ask a question about the course —
    /// never a full narrated lecture. Ignored for every other path.</summary>
    public bool IsQaMode { get; set; }

    /// <summary>AI Agents course chat's "Talk about this" voice path
    /// (2026-09-04) — provide this ALONE, instead of every other target
    /// field, takes priority over all of them. Grounds the voice Agent in
    /// the SAME question/answer/engrams the text chat already produced
    /// (StructuredLearningKnowledgeRetrievalService's own reranked
    /// top-3), so voice never re-runs retrieval or risks disagreeing with
    /// the text answer already on screen.</summary>
    public AiAgentQuestionContext? AiAgentQuestion { get; set; }
}

/// <summary>See <see cref="VoiceTutorSessionRequest.AiAgentQuestion"/>.</summary>
public sealed class AiAgentQuestionContext
{
    public Guid? DocumentId { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    public List<AiAgentQuestionEngramDto> Engrams { get; set; } = [];
}

/// <summary>A single cited engram's excerpt, exactly as already shown to
/// the student as a citation chip in the text chat — no re-fetch.</summary>
public sealed class AiAgentQuestionEngramDto
{
    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

public sealed class VoiceTutorSessionResponseDto
{
    /// <summary>Short-lived Azure OpenAI Realtime ephemeral token — safe to
    /// send to the browser, NEVER the real API key. The browser uses this
    /// directly (Bearer auth) against <see cref="RealtimeCallsUrl"/> to
    /// negotiate a WebRTC session straight with Azure.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    public string RealtimeCallsUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string Voice { get; set; } = string.Empty;

    public long? ExpiresAtUnixSeconds { get; set; }

    /// <summary>Only populated by "Reactivar Engrama" (EngramReviewSessionFunction),
    /// null for the plain Voice Tutor. This node's ALREADY-GENERATED
    /// illustrations, sent straight back in the HTTP response so the
    /// frontend can display them immediately (GetIllustrationImageFunction
    /// URLs), rather than depending solely on the model choosing to call
    /// the show_visual_aid Realtime tool — added 2026-07-24 after live
    /// testing showed the tool-call round trip is unreliable (the model
    /// sometimes claims a visual was shown when the frontend never
    /// received/rendered the corresponding tool call).</summary>
    public List<IllustrationRefDto>? Illustrations { get; set; }
}

/// <summary>Minimal illustration reference for <see cref="VoiceTutorSessionResponseDto.Illustrations"/> — just enough for the frontend to build a GetIllustrationImageFunction URL and a caption, no image bytes.</summary>
public sealed class IllustrationRefDto
{
    public Guid Id { get; set; }

    public string? Caption { get; set; }
}
