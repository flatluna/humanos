using HumanOS.Agents.Runtime;

namespace HumanOS.Agentic.Runtime;

/// <summary>
/// One recorded, auditable stage change (fixed Paso 2, 2026-07-14 — "estados
/// auditables" per user requirement). The Runtime — never the Tutor Agent —
/// is the only writer of these entries; they exist purely for audit/replay,
/// same spirit as Studio's progress events. Persisting this history to SQL
/// is a later Paso (Persistencia) — deliberately NOT designed yet.
/// </summary>
public sealed class RuntimeStageTransition
{
    public RuntimeStage Stage { get; init; }

    public DateTime OccurredDate { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// The full working state threaded through the Runtime Workflow graph for
/// one module session (fixed Paso 2, 2026-07-14). Carries the
/// <see cref="RuntimeSession"/> itself plus its auditable transition
/// history. A fresh <see cref="RuntimeSessionState"/> instance is created
/// once (by <c>ModuleStartedExecutor</c>) and flows/mutates through every
/// executor in the graph — the Tutor Agent (Paso 4+) will read from it but
/// never write <see cref="Agents.Runtime.RuntimeSession.Stage"/> directly.
/// </summary>
public sealed class RuntimeSessionState
{
    public RuntimeSession Session { get; set; } = null!;

    public List<RuntimeStageTransition> History { get; set; } = [];

    /// <summary>How many times <see cref="Agents.Runtime.RuntimeStage.LearnerProduction"/>
    /// has been retried after a non-Verified Assessment (fixed Paso 8,
    /// 2026-07-14) — 0 on the first attempt, bounded by
    /// <c>RuntimeSessionWorkflowFactory.MaxRetries</c>, same bounded-retry
    /// shape as Studio's <c>ModuleWorkItem.Attempt</c>.</summary>
    public int ProductionAttempt { get; set; }

    /// <summary>How many times <see cref="Agents.Runtime.RuntimeStage.RecallRequired"/>
    /// has looped back for another retrieval attempt after an insufficient
    /// <see cref="Agents.Runtime.RecallCheckResult"/> (fixed 2026-07-16) —
    /// 0 on the first attempt, bounded by
    /// <c>RuntimeSessionWorkflowFactory.MaxRecallRetries</c>. Same
    /// bounded-retry shape as <see cref="ProductionAttempt"/>, but for
    /// iterative retrieval practice rather than TargetMetric verification.</summary>
    public int RecallAttempt { get; set; }

    /// <summary>How many times <see cref="Agents.Runtime.RuntimeStage.ChapterRecall"/>
    /// has looped back for another retrieval attempt on the CURRENT
    /// chapter, after an insufficient <see cref="Agents.Runtime.RecallCheckResult"/>
    /// (fixed 2026-07-16 — closes the exact gap the product owner flagged:
    /// a chapter's Recall submission was silently advancing with zero
    /// feedback). Deliberately SEPARATE from <see cref="RecallAttempt"/>
    /// (the module-wide, post-Chapters Recall counter) so retries on one
    /// chapter never eat into the budget of another. Reset to 0 by
    /// <c>ChapterAdvanceExecutor</c> whenever a new chapter starts.</summary>
    public int ChapterRecallAttempt { get; set; }

    /// <summary>Which sub-question of the primary-weight chapter's
    /// <c>PredictionPrompt</c> is being asked THIS turn (fixed 2026-07-17
    /// — closes a real production gap: some stored PredictionPrompts were
    /// authored as multi-part numbered questionnaires instead of ONE
    /// question, and got read aloud in a single breath). 0 on the first
    /// sub-question; incremented by <c>ChapterPredictionEvidenceReceivedExecutor</c>
    /// after each answer. See <see cref="Agentic.Runtime.MultiPartPromptSegmenter"/>
    /// for how the stored prompt is split into sub-questions.</summary>
    public int PredictionDialogueTurn { get; set; }

    /// <summary>Which item of a multi-part <see cref="Agents.Runtime.RuntimePedagogicalContract.LearnerTask"/>
    /// is being presented THIS turn (fixed 2026-07-17 — same mechanism as
    /// <see cref="PredictionDialogueTurn"/>, applied to LearnerProduction:
    /// presents one concrete exercise item at a time instead of dumping
    /// all of them into a single worksheet-style turn). 0 on the first
    /// item; incremented by <c>ProductionEvidenceReceivedExecutor</c>
    /// after each item's evidence is submitted.</summary>
    public int ProductionItemTurn { get; set; }

    /// <summary>Set from the most recent <see cref="EvidenceSubmission.ForceAdvance"/>
    /// (fixed 2026-07-17) — read and cleared by
    /// <see cref="Agentic.Runtime.ChapterRecallCheckExecutor"/>/<see cref="Agentic.Runtime.RecallCheckExecutor"/>
    /// to bypass the Tutor's completeness check entirely when the learner
    /// explicitly chose to move on. Harmless/unused for every other
    /// stage's evidence-received executor.</summary>
    public bool PendingForceAdvance { get; set; }

    /// <summary>Set when a Recall retry budget is exhausted WITHOUT the
    /// learner ever reaching a sufficient attempt (fixed 2026-07-17 —
    /// explicit user request: "llegar a un punto donde el agente
    /// simplemente le da la respuesta correcta cuando ya llevamos un x
    /// numero de iteraciones"). Holds a short, deterministic reveal of the
    /// real source content (no extra LLM call — reuses the exact stored
    /// text, avoiding any risk of the model inventing wrong content).
    /// Consumed and cleared by whichever executor presents the NEXT turn
    /// (<c>PredictionRequiredExecutor</c>/<c>ChapterTeachingExecutor</c>/
    /// <c>RecallRequiredExecutor</c>), which prepends it to their own
    /// message — no new pause-stage/graph-topology change needed.</summary>
    public string? PendingRecallReveal { get; set; }

    /// <summary>The most recent Assessment verdict (fixed Paso 8,
    /// 2026-07-14) — kept for observability/progression routing; not yet
    /// persisted to a DB schema (mirrors the "persistencia de dominio"
    /// deferral from Paso 3).</summary>
    public Agents.Runtime.RuntimeAssessmentResult? LastAssessment { get; set; }

    /// <summary>Which <see cref="Agents.Runtime.RuntimePedagogicalContract.Chapters"/>
    /// entry the session is currently presenting (fixed 2026-07-16) — 0
    /// on the first chapter, incremented by <c>ChapterAdvanceExecutor</c>
    /// after that chapter's Recall (and, for the primary-weight chapter,
    /// Prediction/MiniPractice) turns are done. Unused/irrelevant when
    /// <see cref="Agents.Runtime.RuntimePedagogicalContract.Chapters"/> is
    /// empty (legacy whole-script path).</summary>
    public int CurrentChapterIndex { get; set; }
}

/// <summary>
/// Sent to the learner (via a dedicated <c>RequestPort</c> per pause-stage,
/// see <c>RuntimeSessionWorkflowFactory</c>) whenever the Runtime reaches a
/// stage that requires real <see cref="Agents.Runtime.StudentEvidence"/> —
/// Recall, Prediction, LearnerProduction, Reflection. The <c>Prompt</c> is
/// a STUB in Paso 2 (built directly from the Studio-approved
/// <see cref="RuntimePedagogicalContract"/> fields) — a real Tutor Agent
/// (Paso 4+) will replace this with genuine, adaptive phrasing without
/// changing this contract's shape.
/// </summary>
public sealed class EvidenceRequest
{
    public Guid RuntimeSessionId { get; init; }

    /// <summary>Carried alongside <see cref="RuntimeSessionId"/> (fixed
    /// Paso 9, 2026-07-15) so an HTTP API layer can build a matching
    /// <see cref="Agents.Runtime.StudentEvidence"/> without ever needing to
    /// inspect the Workflow engine's own opaque checkpoint state.</summary>
    public Guid CapabilityModuleId { get; init; }

    public RuntimeStage Stage { get; init; }

    public string Prompt { get; init; } = string.Empty;

    /// <summary>UI-only header display fields (fixed 2026-07-16) — mirror
    /// <see cref="Agents.Runtime.RuntimePedagogicalContract.CapabilityTitle"/>/
    /// <c>CapabilityCode</c>, carried on EVERY evidence-pausing stage (not
    /// just presentation ones) so the caller never has to fall back to a
    /// hardcoded placeholder.</summary>
    public string CapabilityTitle { get; init; } = string.Empty;

    public string CapabilityCode { get; init; } = string.Empty;

    /// <summary>The parent Capability's id (fixed 2026-07-16) — see
    /// <see cref="Agents.Runtime.RuntimePedagogicalContract.CapabilityId"/>.</summary>
    public Guid CapabilityId { get; init; }

    /// <summary>Every chapter's title, in order (fixed 2026-07-16) — lets
    /// a course-sidebar UI render the full phase list and highlight the
    /// active one via <see cref="ChapterIndex"/>, without a second API
    /// call. Empty for legacy modules with no Chapters.</summary>
    public IReadOnlyList<string> AllChapterTitles { get; init; } = [];

    /// <summary>Populated ONLY for <see cref="RuntimeStage.ChapterRecall"/>/
    /// <see cref="RuntimeStage.ChapterPrediction"/> (fixed 2026-07-16) —
    /// null for every other stage, same lazy convention as
    /// <see cref="Agents.Runtime.TutorTurnContext.CurrentChapterIndex"/>.</summary>
    public int? ChapterIndex { get; init; }

    public int? TotalChapters { get; init; }

    public string? ChapterTitle { get; init; }

    /// <summary>Populated ONLY for <see cref="RuntimeStage.RecallRequired"/>/
    /// <see cref="RuntimeStage.ChapterRecall"/> (fixed 2026-07-17 —
    /// explicit user request: "pon el numero de iteracion... como esta
    /// construyendo su memoria") — 1-based attempt number for the CURRENT
    /// retrieval-practice turn, so the learner can see their own progress
    /// across retries instead of a silent bounded loop.</summary>
    public int? AttemptNumber { get; init; }

    /// <summary>Total attempts allowed for this Recall
    /// (1 + <see cref="RuntimeSessionWorkflowFactory.MaxRecallRetries"/>) —
    /// same population rule as <see cref="AttemptNumber"/>.</summary>
    public int? TotalAttempts { get; init; }

    /// <summary>The PREVIOUS attempt's estimated accuracy (0-100), only
    /// present starting from the 2nd attempt onward (fixed 2026-07-17) —
    /// null on the very first attempt (nothing to compare against yet) and
    /// null when the previous turn was a genuine clarifying question
    /// rather than a real attempt (see
    /// <see cref="Agents.Runtime.RecallCheckResult.IsGenuineAttempt"/>).</summary>
    public int? LastAccuracyPercentage { get; init; }
}

/// <summary>
/// The learner's (or, for now, a test harness's) response resuming a
/// paused Runtime session — analogous to Studio's <c>GateDecision</c>, but
/// for evidence submission instead of human approval.
/// </summary>
public sealed class EvidenceSubmission
{
    public Guid RuntimeSessionId { get; init; }

    public Agents.Runtime.StudentEvidence Evidence { get; init; } = null!;

    /// <summary>The learner explicitly chose to move on despite an
    /// insufficient Recall/Prediction check, instead of taking another
    /// bounded retry (fixed 2026-07-17 — explicit user request for a
    /// manual "continuar de todas formas" escape hatch, independent of
    /// <c>RuntimeSessionWorkflowFactory.MaxRecallRetries</c>). Ignored by
    /// every stage except the Recall-check loop
    /// (<see cref="Agentic.Runtime.ChapterRecallCheckExecutor"/>/<see cref="Agentic.Runtime.RecallCheckExecutor"/>),
    /// which treat it as an automatic <c>IsSufficient = true</c> without
    /// even calling the Tutor Agent.</summary>
    public bool ForceAdvance { get; init; }
}

/// <summary>
/// Wraps the Tutor Agent's Assessment verdict together with the session
/// state it was produced for (fixed Paso 8, 2026-07-14) — the routing
/// message conditional edges dispatch on after <c>AssessmentExecutor</c>,
/// same "outcome wrapper + downstream unwrap" shape Studio established for
/// <c>Gate1Outcome</c>/<c>ModuleRouterOutput</c>.
/// </summary>
public sealed class AssessmentOutcome
{
    public RuntimeSessionState State { get; init; } = null!;

    public Agents.Runtime.RuntimeAssessmentResult Result { get; init; } = null!;
}

/// <summary>
/// Wraps the Tutor Agent's lightweight Recall completeness check together
/// with the session state it was produced for (fixed 2026-07-16) — same
/// "outcome wrapper + downstream unwrap" shape as <see cref="AssessmentOutcome"/>,
/// but for the iterative Recall retrieval-practice loop rather than formal
/// TargetMetric verification. See <see cref="Agents.Runtime.RecallCheckResult"/>.
/// </summary>
public sealed class RecallCheckOutcome
{
    public RuntimeSessionState State { get; init; } = null!;

    public Agents.Runtime.RecallCheckResult Result { get; init; } = null!;
}

/// <summary>
/// Sent to the learner when the Runtime reaches <see cref="RuntimeStage.Instruction"/>
/// (fixed Paso 9, 2026-07-15) — the Tutor Agent's real, adaptive phrasing
/// of the module's <see cref="RuntimePedagogicalContract.ModuleScript"/>.
/// Deliberately a DISTINCT type from <see cref="EvidenceRequest"/>: presenting
/// content is not asking for evidence (see <see cref="RuntimeStage.Instruction"/>'s
/// own doc comment — "consumes attention, does not produce evidence").
/// </summary>
public sealed class InstructionPresentation
{
    public Guid RuntimeSessionId { get; init; }

    public string Content { get; init; } = string.Empty;

    /// <summary>UI-only header display fields (fixed 2026-07-16) — see
    /// <see cref="EvidenceRequest.CapabilityTitle"/>.</summary>
    public string CapabilityTitle { get; init; } = string.Empty;

    public string CapabilityCode { get; init; } = string.Empty;

    public Guid CapabilityId { get; init; }

    public Guid CapabilityModuleId { get; init; }
}

/// <summary>
/// The learner's simple acknowledgement that they have read/received the
/// Instruction content and are ready to proceed to LearnerProduction
/// (fixed Paso 9, 2026-07-15) — carries no <see cref="Agents.Runtime.StudentEvidence"/>,
/// matching the "Instruction never produces evidence" rule.
/// </summary>
public sealed class InstructionAcknowledgement
{
    public Guid RuntimeSessionId { get; init; }
}

/// <summary>
/// Sent to the learner ONCE when a module first starts, BEFORE any Recall
/// attempt (fixed 2026-07-16 — closes the "asked to recall something they
/// were never taught" gap: a total beginner has nothing to retrieve on
/// their very first module, so pausing here with a warm, grounded
/// orientation from the Tutor Agent — what this module covers, and a
/// reassurance that not knowing yet is expected — comes before, not
/// instead of, the Recall attempt). Deliberately a DISTINCT type from
/// <see cref="EvidenceRequest"/>: presenting an introduction is not asking
/// for evidence, same rationale as <see cref="InstructionPresentation"/>.
/// </summary>
public sealed class IntroductionPresentation
{
    public Guid RuntimeSessionId { get; init; }

    /// <summary>Deliberately named differently from
    /// <see cref="InstructionPresentation.Content"/> (fixed 2026-07-16) —
    /// both types would otherwise be structurally identical
    /// ({RuntimeSessionId, Content}), and <c>RuntimeApiEngine.DrainAsync</c>
    /// discriminates pending request types by permissive deserialization +
    /// checking a property is non-empty (see that method's own doc
    /// comment) — two types with the SAME property name would each
    /// "successfully" deserialize the other's payload too, silently
    /// misclassifying an Introduction pause as an Instruction pause (wrong
    /// <c>Stage</c> in the API response).</summary>
    public string IntroductionText { get; init; } = string.Empty;

    /// <summary>UI-only header display fields (fixed 2026-07-16) — see
    /// <see cref="EvidenceRequest.CapabilityTitle"/>.</summary>
    public string CapabilityTitle { get; init; } = string.Empty;

    public string CapabilityCode { get; init; } = string.Empty;

    public Guid CapabilityId { get; init; }

    public Guid CapabilityModuleId { get; init; }
}

/// <summary>
/// The learner's simple acknowledgement that they have read the module
/// introduction and are ready to proceed to RecallRequired (fixed
/// 2026-07-16) — carries no <see cref="Agents.Runtime.StudentEvidence"/>,
/// mirrors <see cref="InstructionAcknowledgement"/> exactly.
/// </summary>
public sealed class IntroductionAcknowledgement
{
    public Guid RuntimeSessionId { get; init; }
}

/// <summary>
/// Sent to the learner when the Runtime reaches <see cref="RuntimeStage.ChapterTeaching"/>
/// for the CURRENT chapter (fixed 2026-07-16) — the phase-based replacement
/// for <see cref="InstructionPresentation"/> when the module has Chapters.
/// Deliberately does NOT reuse <c>Content</c> as its text property name
/// (see <see cref="IntroductionPresentation.IntroductionText"/>'s doc
/// comment for why two structurally-identical types risk being
/// misclassified by <c>RuntimeApiEngine.DrainAsync</c>'s permissive
/// deserialization).
/// </summary>
public sealed class ChapterPresentation
{
    public Guid RuntimeSessionId { get; init; }

    public int ChapterIndex { get; init; }

    public int TotalChapters { get; init; }

    public string ChapterTitle { get; init; } = string.Empty;

    public string TeachingContent { get; init; } = string.Empty;

    /// <summary>UI-only header display fields (fixed 2026-07-16) — see
    /// <see cref="EvidenceRequest.CapabilityTitle"/>.</summary>
    public string CapabilityTitle { get; init; } = string.Empty;

    public string CapabilityCode { get; init; } = string.Empty;

    public Guid CapabilityId { get; init; }

    public Guid CapabilityModuleId { get; init; }

    public IReadOnlyList<string> AllChapterTitles { get; init; } = [];
}

/// <summary>
/// The learner's simple acknowledgement that they have read the current
/// chapter's teaching content (fixed 2026-07-16) — mirrors
/// <see cref="InstructionAcknowledgement"/> exactly; carries no
/// <see cref="Agents.Runtime.StudentEvidence"/>.
/// </summary>
public sealed class ChapterAcknowledgement
{
    public Guid RuntimeSessionId { get; init; }
}

/// <summary>
/// Sent to the learner when the Runtime reaches
/// <see cref="RuntimeStage.ChapterMiniPractice"/> (fixed 2026-07-16) —
/// only for the module's ONE primary-weight chapter, right after its
/// <see cref="RuntimeStage.ChapterPrediction"/> turn. Presentation-only,
/// same rationale as <see cref="ChapterPresentation"/>; uses a uniquely-
/// named text property for the same deserialization-ambiguity reason.
/// </summary>
public sealed class ChapterMiniPracticePresentation
{
    public Guid RuntimeSessionId { get; init; }

    public string ChapterTitle { get; init; } = string.Empty;

    public string MiniPracticeContent { get; init; } = string.Empty;

    /// <summary>UI-only header display fields (fixed 2026-07-16) — see
    /// <see cref="EvidenceRequest.CapabilityTitle"/>.</summary>
    public string CapabilityTitle { get; init; } = string.Empty;

    public string CapabilityCode { get; init; } = string.Empty;

    public Guid CapabilityId { get; init; }

    public Guid CapabilityModuleId { get; init; }
}

/// <summary>
/// The learner's simple acknowledgement that they attempted the current
/// chapter's mini-practice off-app (fixed 2026-07-16) — mirrors
/// <see cref="ChapterAcknowledgement"/> exactly; carries no
/// <see cref="Agents.Runtime.StudentEvidence"/> (this is private retrieval
/// practice, never graded).
/// </summary>
public sealed class ChapterMiniPracticeAcknowledgement
{
    public Guid RuntimeSessionId { get; init; }
}
