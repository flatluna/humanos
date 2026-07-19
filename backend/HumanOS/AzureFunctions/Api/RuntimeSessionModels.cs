using HumanOS.Agentic.Runtime;
using HumanOS.Agents.Runtime;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Request body for starting a new Interactive Learning Runtime session
/// (Paso 9, 2026-07-15) — <c>PersonId</c> comes from the route (TODO:
/// derive from the validated Entra token, same open item as
/// <c>StartAssessmentAttemptFunction</c>), <c>CapabilityModuleId</c> too.
/// No request body properties needed today; kept as a placeholder for a
/// future explicit "resume where I left off across modules" hint.
/// </summary>
public sealed class StartRuntimeSessionRequest
{
}

/// <summary>
/// One part of the learner's submitted evidence, as received over HTTP —
/// mirrors <see cref="StudentEvidencePart"/> exactly.
/// </summary>
public sealed class SubmitRuntimeEvidencePartRequest
{
    public StudentEvidenceKind Kind { get; init; }

    public string? Text { get; init; }

    public string? StorageUrl { get; init; }

    public string? MimeType { get; init; }
}

/// <summary>
/// Request body for submitting evidence to the CURRENTLY pending Runtime
/// stage (Paso 9, 2026-07-15). Deliberately does NOT include
/// <c>Origin</c> — the Runtime derives it from the pending
/// <see cref="EvidenceRequest.Stage"/> (see <see cref="RuntimeApiEngine.MapStageToOrigin"/>),
/// never trusting a caller-supplied label.
/// </summary>
public sealed class SubmitRuntimeEvidenceRequest
{
    public List<SubmitRuntimeEvidencePartRequest> Parts { get; init; } = [];

    public EvidenceAssistanceLevel AssistanceLevel { get; init; }

    /// <summary>Ignored (forced <see langword="true"/>) when the pending
    /// stage is Recall/Prediction — see <see cref="RuntimeApiEngine.BuildEvidence"/>.</summary>
    public bool CapturedBeforeAssistance { get; init; }

    public Guid? ComparesToEvidenceId { get; init; }

    /// <summary>The learner explicitly chose to move on despite an
    /// insufficient Recall/Prediction check, instead of taking another
    /// bounded retry (fixed 2026-07-17) — see
    /// <see cref="EvidenceSubmission.ForceAdvance"/>. Ignored by every
    /// stage except the Recall-check loop.</summary>
    public bool ForceAdvance { get; init; }
}

/// <summary>
/// The single response shape returned by every Runtime session endpoint
/// (Paso 9, 2026-07-15) — a thin, API-facing projection of whatever
/// <see cref="RuntimeApiEngine.DrainAsync"/> paused or terminated on.
/// </summary>
public sealed class RuntimeTurnResponse
{
    public Guid RuntimeSessionId { get; init; }

    public RuntimeStage Stage { get; init; }

    /// <summary>The Tutor Agent's real, adaptive text for this turn —
    /// either a question/instruction prompt (evidence-pausing stages) or
    /// the phrased module content (Instruction).</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>True when <see cref="Stage"/> is <c>Instruction</c> or
    /// <c>ModuleStarted</c> (fixed 2026-07-16) — tells the caller to call
    /// the instruction-ack/introduction-ack endpoint next instead of
    /// submitting evidence. The name predates the ModuleStarted
    /// introduction pause but the semantics generalize cleanly: "just
    /// acknowledge, don't submit evidence".</summary>
    public bool RequiresInstructionAcknowledgementOnly { get; init; }

    public bool IsTerminal { get; init; }

    /// <summary>0-based index of the chapter currently being presented
    /// (fixed 2026-07-16) — populated only when <see cref="Stage"/> is one
    /// of the <c>Chapter*</c> stages; null otherwise (legacy whole-script
    /// path, or any non-chapter stage).</summary>
    public int? ChapterIndex { get; init; }

    /// <summary>Total chapter count for this module (fixed 2026-07-16) —
    /// same population rule as <see cref="ChapterIndex"/>, lets the caller
    /// render e.g. "Capítulo 2 de 5".</summary>
    public int? TotalChapters { get; init; }

    /// <summary>The active chapter's title (fixed 2026-07-16) — same
    /// population rule as <see cref="ChapterIndex"/>.</summary>
    public string? ChapterTitle { get; init; }

    /// <summary>1-based attempt number for the current Recall retrieval-
    /// practice turn (fixed 2026-07-17) — populated only for
    /// RecallRequired/ChapterRecall. Lets the UI show e.g. "Intento 2 de
    /// 5" instead of a silent bounded loop.</summary>
    public int? AttemptNumber { get; init; }

    /// <summary>Total attempts allowed (fixed 2026-07-17) — same
    /// population rule as <see cref="AttemptNumber"/>.</summary>
    public int? TotalAttempts { get; init; }

    /// <summary>The PREVIOUS attempt's estimated accuracy 0-100 (fixed
    /// 2026-07-17) — null on the first attempt or after a genuine
    /// clarifying question. Lets the UI show the learner's own retrieval-
    /// practice progress across attempts.</summary>
    public int? LastAccuracyPercentage { get; init; }

    /// <summary>Every chapter's title for this module, in order (fixed
    /// 2026-07-16) — always populated (even outside Chapter* stages) so a
    /// course-sidebar UI can render the full phase sub-list for the
    /// CURRENT module without an extra call. Empty for legacy modules
    /// with no Chapters.</summary>
    public IReadOnlyList<string> AllChapterTitles { get; init; } = [];

    /// <summary>The Capability this module belongs to (fixed 2026-07-16) —
    /// lets a caller fetch <c>GET /capabilities/{id}/content</c> to render
    /// the full course-wide sidebar (all levels/modules).</summary>
    public Guid CapabilityId { get; init; }

    public Guid CapabilityModuleId { get; init; }

    /// <summary>UI-only header display (fixed 2026-07-16) — the real
    /// Capability name/code, never a hardcoded placeholder.</summary>
    public string CapabilityTitle { get; init; } = string.Empty;

    public string CapabilityCode { get; init; } = string.Empty;

    /// <summary>Populated only for a terminal turn (<c>Completed</c> or
    /// <c>RequiresRevision</c>) when at least one Assessment ran this
    /// session.</summary>
    public RuntimeAssessmentResult? FinalAssessment { get; init; }

    internal static RuntimeTurnResponse From(RuntimeDrainResult drain, Guid runtimeSessionId)
    {
        if (drain.Output is { } state)
        {
            var message = state.Session.Stage == RuntimeStage.RequiresRevision
                ? state.LastAssessment?.Explanation
                    ?? "Este módulo requiere revisión — no se alcanzó la métrica objetivo."
                : "Módulo completado.";

            return new RuntimeTurnResponse
            {
                RuntimeSessionId = runtimeSessionId,
                Stage = state.Session.Stage,
                Message = message,
                IsTerminal = true,
                FinalAssessment = state.LastAssessment,
                CapabilityId = state.Session.Contract.CapabilityId,
                CapabilityModuleId = state.Session.Contract.CapabilityModuleId,
                CapabilityTitle = state.Session.Contract.CapabilityTitle,
                CapabilityCode = state.Session.Contract.CapabilityCode,
                AllChapterTitles = [.. state.Session.Contract.Chapters.Select(c => c.Title)]
            };
        }

        if (drain.EvidenceRequest is { } evidenceRequest)
        {
            return new RuntimeTurnResponse
            {
                RuntimeSessionId = runtimeSessionId,
                Stage = evidenceRequest.Stage,
                Message = evidenceRequest.Prompt,
                ChapterIndex = evidenceRequest.ChapterIndex,
                TotalChapters = evidenceRequest.TotalChapters,
                ChapterTitle = evidenceRequest.ChapterTitle,
                AttemptNumber = evidenceRequest.AttemptNumber,
                TotalAttempts = evidenceRequest.TotalAttempts,
                LastAccuracyPercentage = evidenceRequest.LastAccuracyPercentage,
                AllChapterTitles = evidenceRequest.AllChapterTitles,
                CapabilityId = evidenceRequest.CapabilityId,
                CapabilityModuleId = evidenceRequest.CapabilityModuleId,
                CapabilityTitle = evidenceRequest.CapabilityTitle,
                CapabilityCode = evidenceRequest.CapabilityCode
            };
        }

        if (drain.InstructionPresentation is { } instruction)
        {
            return new RuntimeTurnResponse
            {
                RuntimeSessionId = runtimeSessionId,
                Stage = RuntimeStage.Instruction,
                Message = instruction.Content,
                RequiresInstructionAcknowledgementOnly = true,
                CapabilityId = instruction.CapabilityId,
                CapabilityModuleId = instruction.CapabilityModuleId,
                CapabilityTitle = instruction.CapabilityTitle,
                CapabilityCode = instruction.CapabilityCode
            };
        }

        if (drain.IntroductionPresentation is { } introduction)
        {
            return new RuntimeTurnResponse
            {
                RuntimeSessionId = runtimeSessionId,
                Stage = RuntimeStage.ModuleStarted,
                Message = introduction.IntroductionText,
                RequiresInstructionAcknowledgementOnly = true,
                CapabilityId = introduction.CapabilityId,
                CapabilityModuleId = introduction.CapabilityModuleId,
                CapabilityTitle = introduction.CapabilityTitle,
                CapabilityCode = introduction.CapabilityCode
            };
        }

        if (drain.ChapterPresentation is { } chapter)
        {
            return new RuntimeTurnResponse
            {
                RuntimeSessionId = runtimeSessionId,
                Stage = RuntimeStage.ChapterTeaching,
                Message = chapter.TeachingContent,
                RequiresInstructionAcknowledgementOnly = true,
                ChapterIndex = chapter.ChapterIndex,
                TotalChapters = chapter.TotalChapters,
                ChapterTitle = chapter.ChapterTitle,
                AllChapterTitles = chapter.AllChapterTitles,
                CapabilityId = chapter.CapabilityId,
                CapabilityModuleId = chapter.CapabilityModuleId,
                CapabilityTitle = chapter.CapabilityTitle,
                CapabilityCode = chapter.CapabilityCode
            };
        }

        if (drain.ChapterMiniPracticePresentation is { } chapterMiniPractice)
        {
            return new RuntimeTurnResponse
            {
                RuntimeSessionId = runtimeSessionId,
                Stage = RuntimeStage.ChapterMiniPractice,
                Message = chapterMiniPractice.MiniPracticeContent,
                RequiresInstructionAcknowledgementOnly = true,
                ChapterTitle = chapterMiniPractice.ChapterTitle,
                CapabilityId = chapterMiniPractice.CapabilityId,
                CapabilityModuleId = chapterMiniPractice.CapabilityModuleId,
                CapabilityTitle = chapterMiniPractice.CapabilityTitle,
                CapabilityCode = chapterMiniPractice.CapabilityCode
            };
        }

        throw new InvalidOperationException("RuntimeDrainResult had neither a pending request nor an output.");
    }

    /// <summary>Builds a terminal-only response from the technical
    /// <see cref="HumanOS.Data.RuntimeSessionStatus"/> pointer (fixed Paso
    /// 9, 2026-07-15) — used to short-circuit a repeat call on an
    /// already-finished session WITHOUT ever calling
    /// <c>ResumeStreamingAsync</c> again (see that type's doc comment for
    /// the resume-hang bug this avoids). Deliberately carries no
    /// <see cref="FinalAssessment"/> — that rich detail is only available
    /// from the ORIGINAL call that observed the real terminal output.</summary>
    internal static RuntimeTurnResponse FromTerminalStatus(
        Guid runtimeSessionId, HumanOS.Data.RuntimeSessionStatus status)
    {
        var stage = Enum.TryParse<RuntimeStage>(status.FinalStage, out var parsed)
            ? parsed
            : RuntimeStage.Completed;

        return new RuntimeTurnResponse
        {
            RuntimeSessionId = runtimeSessionId,
            Stage = stage,
            Message = stage == RuntimeStage.RequiresRevision
                ? "Este módulo requiere revisión — no se alcanzó la métrica objetivo."
                : "Módulo completado.",
            IsTerminal = true
        };
    }
}
