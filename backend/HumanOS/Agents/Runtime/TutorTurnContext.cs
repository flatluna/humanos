using System.Text.Json.Serialization;

namespace HumanOS.Agents.Runtime;

/// <summary>
/// A concrete capability the Tutor Agent may invoke as a tool — deliberately
/// a small, explicit enum rather than a free-form string set, so the
/// Runtime's per-stage gating logic (<see cref="TutorTurnContextBuilder"/>)
/// stays exhaustive and compiler-checked as new tools are added. Only
/// covers tools already decided in principle (see
/// /memories/repo/human-os-runtime-design.md) — none of these are
/// implemented yet (Paso 7).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TutorTool
{
    /// <summary>Deterministic arithmetic verification — never trust the
    /// LLM's own mental math for grading (same LLM+validator philosophy
    /// as Assessment).</summary>
    Calculator,

    /// <summary>Reads a learner-provided or module-provided table/
    /// spreadsheet into structured data.</summary>
    TableReader
}

/// <summary>
/// What the Tutor Agent is allowed to do THIS turn — computed by the
/// Runtime (<see cref="TutorTurnContextBuilder"/>) from
/// <see cref="RuntimeStage"/>, never by the Tutor's own judgment. This is
/// the concrete, structural mechanism (not a prompt instruction) that
/// enforces the Memory Paradox's anti-offloading rule: e.g. during
/// <see cref="RuntimeStage.RecallRequired"/>/<see cref="RuntimeStage.PredictionRequired"/>,
/// <see cref="KnowledgeAccessAllowed"/> is hard-coded <see langword="false"/>
/// regardless of prompt wording — the Tutor cannot bypass this by being
/// asked cleverly, because the capability is withheld at the framework
/// level, not merely discouraged.
/// </summary>
public sealed class TutorToolPermissions
{
    /// <summary>Whether the Tutor may look up <c>CapabilityKnowledgeChunk</c>
    /// content (per-module script chunks + the capability-wide
    /// TutorKnowledgeBase overview) via RAG this turn.</summary>
    public bool KnowledgeAccessAllowed { get; init; }

    public IReadOnlyList<TutorTool> AllowedTools { get; init; } = [];
}

/// <summary>
/// The single, minimal, explicit contract between the Interactive Learning
/// Runtime and the Tutor Agent (fixed 2026-07-14, before Paso 4's actual
/// agent code — see /memories/repo/human-os-runtime-design.md). Built
/// fresh by <see cref="TutorTurnContextBuilder"/> on every turn; the Tutor
/// Agent receives this as its ONLY window into Runtime state — it must
/// never reach into <see cref="RuntimeSession"/>/<see cref="RuntimeSessionState"/>
/// directly.
/// </summary>
/// <remarks>
/// Deliberately excludes: other learners' data, future modules/levels not
/// yet reached, global/cross-capability metrics, and the learner's
/// PersonId (not needed for a single pedagogical turn — keeps this
/// privacy-conscious by construction, consistent with the "Private to You"
/// default in adaptive-learning-engine-design.md).
/// </remarks>
public sealed class TutorTurnContext
{
    public Guid RuntimeSessionId { get; init; }

    public RuntimeStage CurrentStage { get; init; }

    /// <summary>The fixed, Studio-approved pedagogical contract for the
    /// current module — the Tutor may read this but never reinterpret or
    /// override it.</summary>
    public RuntimePedagogicalContract Contract { get; init; } = null!;

    /// <summary>All evidence captured so far THIS session, in capture
    /// order — lets the Tutor compare e.g. a Reflection against an
    /// earlier Prediction without re-querying anything itself.</summary>
    public IReadOnlyList<StudentEvidence> AccumulatedEvidence { get; init; } = [];

    /// <summary>The module's instructional script — populated ONLY when
    /// <see cref="CurrentStage"/> is <see cref="RuntimeStage.Instruction"/>
    /// (lazy by design: no reason to carry this around during
    /// Recall/Prediction/Production/Reflection turns).</summary>
    public string? ModuleScript { get; init; }

    /// <summary>Which <see cref="RuntimePedagogicalContract.Chapters"/>
    /// entry is active this turn (fixed 2026-07-16) — populated ONLY when
    /// <see cref="CurrentStage"/> is one of the <c>Chapter*</c> stages
    /// (<see cref="RuntimeStage.ChapterTeaching"/>/<see cref="RuntimeStage.ChapterRecall"/>/
    /// <see cref="RuntimeStage.ChapterPrediction"/>/<see cref="RuntimeStage.ChapterMiniPractice"/>).
    /// Same lazy-by-design rationale as <see cref="ModuleScript"/>.</summary>
    public int? CurrentChapterIndex { get; init; }

    /// <summary>Overrides the chapter field <c>BuildChapterNote</c> would
    /// otherwise use as source text (fixed 2026-07-17 — micro-dialogue
    /// fix: presents ONE sub-question of a multi-part
    /// <c>PredictionPrompt</c>/<c>RecallPrompt</c> at a time instead of
    /// the whole stored text). Null means "use the chapter's own field for
    /// <see cref="CurrentStage"/> as before" — see
    /// <see cref="Agentic.Runtime.MultiPartPromptSegmenter"/>.</summary>
    public string? ChapterSourceTextOverride { get; init; }

    /// <summary>True when this turn is ONE sub-question of a multi-part
    /// dialogue (fixed 2026-07-17) — tells the Tutor to phrase it as a
    /// single, natural conversational question, never mentioning "part X
    /// of Y" or that more questions follow.</summary>
    public bool IsMultiPartChapterDialogueTurn { get; init; }

    /// <summary>Overrides the concrete task text used to ground a
    /// LearnerProduction turn (fixed 2026-07-17 — closes a real grounding
    /// gap: the Tutor was inventing concrete exercise content, e.g. "las
    /// cinco expresiones", fresh every turn with zero stored grounding).
    /// Set to ONE item of <see cref="RuntimePedagogicalContract.LearnerTask"/>
    /// when it's a multi-part numbered list (see
    /// <see cref="Agentic.Runtime.MultiPartPromptSegmenter"/>); null means
    /// use the whole <see cref="RuntimePedagogicalContract.LearnerTask"/>
    /// as-is (single-item task).</summary>
    public string? LearnerTaskOverride { get; init; }

    /// <summary>True when this LearnerProduction turn is ONE item of a
    /// multi-part task (fixed 2026-07-17) — same "don't reveal it's part X
    /// of Y" rationale as <see cref="IsMultiPartChapterDialogueTurn"/>.</summary>
    public bool IsMultiPartLearnerTaskTurn { get; init; }

    /// <summary>The pedagogical mode the Tutor should operate in this turn
    /// (Paso 5, 2026-07-14) — selected by the Runtime
    /// (<c>TutorSkillSelector</c>) from <see cref="CurrentStage"/> and
    /// <see cref="Contract"/>'s TargetMetric, never chosen by the Tutor
    /// itself. Null when no specific Skill applies (e.g. Instruction,
    /// Assessment).</summary>
    public TutorSkill? ActiveSkill { get; init; }

    /// <summary>The most recent Assessment verdict for this session, if
    /// any (fixed Paso 9, 2026-07-15) — lets the Tutor give constructive,
    /// answer-free feedback when phrasing a <see cref="RuntimeStage.LearnerProduction"/>
    /// retry prompt after a NotVerified/Failed attempt. Null before the
    /// first Assessment ever runs.</summary>
    public RuntimeAssessmentResult? LastAssessment { get; init; }

    public TutorToolPermissions Permissions { get; init; } = new();
}
