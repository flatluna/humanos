using System.Text.Json.Serialization;
using HumanOS.Agents.Studio;

namespace HumanOS.Agents.Runtime;

/// <summary>
/// Paso 1 of the Interactive Learning Runtime (2026-07-14). These are the
/// foundational contracts for the learner-facing Runtime — the layer that
/// sits between a published Capability and the Tutor Agent (see
/// /memories/repo/humanstudio-multiagent-vision.md for the full
/// architecture discussion that led here).
/// </summary>
/// <remarks>
/// GOVERNING PRINCIPLE — THE MEMORY PARADOX (fijado 2026-07-14, debe
/// condicionar TODA decisión futura sobre este Runtime, no solo Paso 1):
/// "La IA debe fortalecer la memoria, el conocimiento, el pensamiento y la
/// autonomía humana, no sustituirlos." Cada tipo en este archivo existe
/// para hacer cumplir esa tesis en código, no solo en prompts:
/// <list type="bullet">
/// <item><description>El Runtime NUNCA representa consumo (video visto,
/// tiempo en pantalla, módulo "completado") como evidencia de progreso —
/// solo <see cref="StudentEvidence"/> cuenta, y solo cuando representa
/// producción real del alumno.</description></item>
/// <item><description>Saber DÓNDE buscar una respuesta no es equivalente a
/// saberla — por eso <see cref="EvidenceAssistanceLevel"/> existe: para
/// distinguir recuperación genuina de simple localización asistida.</description></item>
/// <item><description>El Tutor debe pedir recuperación ANTES de ayudar
/// siempre que sea posible — por eso <see cref="StudentEvidence.CapturedBeforeAssistance"/>
/// existe como campo de primera clase, no como metadato opcional.</description></item>
/// </list>
/// </remarks>
public static class RuntimeGoverningPrinciple
{
}

/// <summary>
/// The deterministic sequence of stages a single module's live learning
/// session moves through (fixed Paso 1, 2026-07-14). This enum IS the
/// Interactive Learning Runtime's state machine vocabulary — the Runtime
/// (code, not the Tutor Agent) owns all transitions between these values.
/// The Tutor Agent only ever operates WITHIN one stage at a time; it never
/// decides which stage comes next (see DECISIÓN 1/2 in
/// /memories/repo/humanstudio-multiagent-vision.md).
/// </summary>
/// <remarks>
/// Paso 1 scope: this enum defines the VOCABULARY only. The actual state
/// machine (valid transitions, guards, revision/failure branches — likely
/// mirroring Studio's <see cref="ModuleProcessingStatus.RequiresRevision"/>
/// distinction) is Paso 2's responsibility. Deliberately not added yet to
/// avoid over-scoping Paso 1.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeStage
{
    /// <summary>Session initialized for this module; the Runtime has loaded
    /// its <see cref="RuntimePedagogicalContract"/> and nothing else has
    /// happened yet.</summary>
    ModuleStarted,

    /// <summary>The learner must attempt unaided/cued retrieval BEFORE any
    /// instruction, example, or AI assistance — implements the module's
    /// <see cref="RuntimePedagogicalContract.RecallRequirement"/>. Produces
    /// <see cref="StudentEvidence"/> with <see cref="StudentEvidenceOrigin.Recall"/>.</summary>
    RecallRequired,

    /// <summary>The learner commits to a prediction/hypothesis BEFORE
    /// seeing the real content or answer (neuroscience principle P1 —
    /// prediction error — already used by InstructorAgent in Studio).
    /// Produces <see cref="StudentEvidence"/> with
    /// <see cref="StudentEvidenceOrigin.Prediction"/>.</summary>
    PredictionRequired,

    /// <summary>Content/explanation is presented. This stage CONSUMES
    /// attention, it does not produce <see cref="StudentEvidence"/> — pure
    /// presentation is never evidence of anything by itself. Used only
    /// when the module has NO <see cref="RuntimePedagogicalContract.Chapters"/>
    /// (legacy/whole-script presentation) — see <see cref="ChapterTeaching"/>
    /// for the phase-based replacement (fixed 2026-07-16).</summary>
    Instruction,

    /// <summary>Presents ONE Chapter's <c>TeachingContent</c> (fixed
    /// 2026-07-16 — the phase-based Runtime presentation the Studio
    /// Chapters feature was built for; see
    /// <see cref="RuntimePedagogicalContract.Chapters"/>). Same
    /// presentation-only semantics as <see cref="Instruction"/> — never
    /// produces <see cref="StudentEvidence"/>. The session loops through
    /// every Chapter in order before ever reaching
    /// <see cref="RecallRequired"/> (the module's FINAL/cumulative
    /// recall).</summary>
    ChapterTeaching,

    /// <summary>Presents the CURRENT chapter's own <c>RecallPrompt</c>
    /// (fixed 2026-07-16) — a lighter, per-chapter retrieval check,
    /// distinct from the module-wide <see cref="RecallRequired"/> that
    /// still runs once, at the end, after every chapter. Produces
    /// <see cref="StudentEvidence"/> with <see cref="StudentEvidenceOrigin.Recall"/>.</summary>
    ChapterRecall,

    /// <summary>Presents the CURRENT chapter's <c>PredictionPrompt</c>
    /// (fixed 2026-07-16) — only reached for the ONE chapter with
    /// <c>IsPrimaryWeight = true</c> (Studio guarantees exactly one).
    /// Produces <see cref="StudentEvidence"/> with
    /// <see cref="StudentEvidenceOrigin.Prediction"/>.</summary>
    ChapterPrediction,

    /// <summary>Presents the CURRENT chapter's <c>MiniPracticePrompt</c>
    /// (fixed 2026-07-16) — only reached right after
    /// <see cref="ChapterPrediction"/>, on the same primary-weight
    /// chapter. Presentation-only, like <see cref="Instruction"/> — the
    /// learner works the exercise off-app (notebook/scratch) and simply
    /// acknowledges to continue; never produces <see cref="StudentEvidence"/>
    /// (this is private retrieval practice, not graded evidence).</summary>
    ChapterMiniPractice,

    /// <summary>The learner produces the concrete, observable artifact
    /// required by <see cref="RuntimePedagogicalContract.LearnerProduction"/>.
    /// The AI must never produce this evidence on the learner's behalf.
    /// Produces <see cref="StudentEvidence"/> with
    /// <see cref="StudentEvidenceOrigin.Production"/>.</summary>
    LearnerProduction,

    /// <summary>The Tutor Agent's judgment (LLM) plus a deterministic
    /// validator (code) evaluate the collected evidence against
    /// <see cref="RuntimePedagogicalContract.SuccessCriteria"/> — same
    /// LLM+validator pattern already proven by Studio's Métrico agent.
    /// This stage CONSUMES evidence, it does not produce new
    /// <see cref="StudentEvidence"/>.</summary>
    Assessment,

    /// <summary>Post-task metacognitive reflection — explicitly compares
    /// what the learner predicted (<see cref="StudentEvidenceOrigin.Prediction"/>)
    /// against what actually happened. Produces <see cref="StudentEvidence"/>
    /// with <see cref="StudentEvidenceOrigin.Reflection"/>.</summary>
    Reflection,

    /// <summary>Terminal stage — the module's session is done. Progression
    /// to the next module is a Runtime decision grounded in the
    /// Assessment outcome, never in time spent or stages "visited".</summary>
    Completed,

    /// <summary>Terminal stage (fixed Paso 8, 2026-07-14) — Assessment did
    /// NOT verify the TargetMetric and the bounded retry budget
    /// (<c>RuntimeSessionWorkflowFactory.MaxRetries</c>) was exhausted.
    /// A legitimate pedagogical outcome, not a technical error — mirrors
    /// Studio's <c>ModuleProcessingStatus.RequiresRevision</c> distinction
    /// from <c>Failed</c>.</summary>
    RequiresRevision
}

/// <summary>
/// WHICH cognitive act a piece of <see cref="StudentEvidence"/> is proof
/// of. Deliberately distinct from <see cref="RuntimeStage"/>: not every
/// stage produces evidence (<see cref="RuntimeStage.Instruction"/> and
/// <see cref="RuntimeStage.Assessment"/> never do — one presents, the
/// other judges) — this enum only has values for stages that DO.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StudentEvidenceOrigin
{
    /// <summary>Unaided or cued retrieval attempt, captured BEFORE
    /// instruction — see <see cref="StudentEvidence.CapturedBeforeAssistance"/>,
    /// which must be <see langword="true"/> for this origin.</summary>
    Recall,

    /// <summary>The learner's own prediction/hypothesis, captured BEFORE
    /// seeing the real content or answer — must be <see langword="true"/>
    /// for <see cref="StudentEvidence.CapturedBeforeAssistance"/> as well.</summary>
    Prediction,

    /// <summary>The concrete, observable artifact demanded by the
    /// module's <see cref="RuntimePedagogicalContract.LearnerProduction"/> —
    /// the primary evidence Assessment evaluates against
    /// <see cref="RuntimePedagogicalContract.SuccessCriteria"/>.</summary>
    Production,

    /// <summary>Post-task metacognitive reflection, typically comparing
    /// against an earlier <see cref="Prediction"/> via
    /// <see cref="StudentEvidence.ComparesToEvidenceId"/>.</summary>
    Reflection
}

/// <summary>
/// The FORMAT of a single <see cref="StudentEvidencePart"/> — deliberately
/// open-ended from Paso 1 onward (per explicit instruction 2026-07-14: do
/// not let StudentEvidence be born as "just a text answer") so future media
/// types (audio transcription, uploaded photos of handwritten work, live
/// project files, spreadsheets) never require a breaking contract change,
/// only a new enum value.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StudentEvidenceKind
{
    /// <summary>Free text authored directly by the learner (an
    /// explanation in their own words, a written answer, code typed
    /// inline, etc.).</summary>
    Text,

    Image,

    Audio,

    /// <summary>An uploaded/attached document (PDF, Word, etc.) authored
    /// or annotated by the learner.</summary>
    Document,

    /// <summary>Structured tabular data (a spreadsheet, a filled-in
    /// table) produced by the learner.</summary>
    Table,

    /// <summary>A larger multi-file artifact (e.g. a project folder, a
    /// repository) produced by the learner.</summary>
    Project,

    /// <summary>Source code authored by the learner, when distinct from
    /// a plain <see cref="Text"/> answer (e.g. an evaluated code
    /// submission).</summary>
    Code,

    /// <summary>Escape hatch for a format not yet modeled explicitly —
    /// must carry a <see cref="StudentEvidencePart.MimeType"/> so it can
    /// still be rendered/routed correctly.</summary>
    Other
}

/// <summary>
/// How much external support was present when a piece of
/// <see cref="StudentEvidence"/> was produced. This is the field that
/// operationalizes "saber dónde buscar una respuesta no es equivalente a
/// saberla" — evidence with heavier assistance is never treated as proof
/// of internalized memory/capability, regardless of how correct it looks.
/// </summary>
/// <remarks>
/// Ordered from least to most assisted. Whether a given
/// <see cref="RuntimeStage"/>/<see cref="StudentEvidenceOrigin"/> combo
/// ACCEPTS a given level (e.g. Recall should almost never accept
/// <see cref="WithAiAssistance"/>) is a Runtime/validator rule for a later
/// Paso — this enum only defines the vocabulary.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EvidenceAssistanceLevel
{
    /// <summary>No external aid whatsoever — the strongest possible
    /// evidence of internalized memory/capability.</summary>
    Unaided,

    /// <summary>Keyword/category cues only, no full answers or worked
    /// examples — mirrors Studio's existing <see cref="RecallSupportLevel.WithCues"/>.</summary>
    WithRetrievalCues,

    /// <summary>Graduated hints, worked examples, or checklists were
    /// available/used — scaffolding, not offloading, but weaker evidence
    /// than <see cref="Unaided"/>.</summary>
    WithGuidedHints,

    /// <summary>The AI materially contributed to producing the content of
    /// this evidence (not just formatting/encouragement). Tracked for
    /// observability and NEVER counted as demonstrating the target
    /// metric — this is the exact scenario the Memory Paradox exists to
    /// prevent, so its presence in a Production/Recall/Prediction piece
    /// of evidence is a structural red flag, not a stylistic detail.</summary>
    WithAiAssistance
}

/// <summary>
/// One unit of a <see cref="StudentEvidence"/> record — the general,
/// extensible building block requested 2026-07-14 so evidence is never
/// hard-coded to plain text. A single <see cref="StudentEvidence"/> can
/// combine multiple parts (e.g. a table plus the learner's own written
/// explanation of it).
/// </summary>
public sealed class StudentEvidencePart
{
    public StudentEvidenceKind Kind { get; init; }

    /// <summary>Present for <see cref="StudentEvidenceKind.Text"/>, or as
    /// a learner-authored caption/transcript alongside a non-text
    /// <see cref="StorageUrl"/> (e.g. the learner's own description of an
    /// uploaded photo — never an AI-generated caption).</summary>
    public string? Text { get; init; }

    /// <summary>Blob reference (Azure Storage, matching the existing
    /// <c>Azure.Storage.Blobs</c> usage elsewhere in this backend) for
    /// non-text formats — never inline raw bytes on this contract.</summary>
    public string? StorageUrl { get; init; }

    public string? MimeType { get; init; }
}

/// <summary>
/// One short, ordered segment of a module's teaching content, phase-based
/// (fixed 2026-07-16) — the Runtime's read-only projection of Studio's
/// <c>CapabilityModuleChapter</c>. See <see cref="RuntimeStage.ChapterTeaching"/>/
/// <see cref="RuntimeStage.ChapterRecall"/>/<see cref="RuntimeStage.ChapterPrediction"/>/
/// <see cref="RuntimeStage.ChapterMiniPractice"/> for how each field is
/// surfaced turn by turn.
/// </summary>
public sealed class RuntimeModuleChapter
{
    public string Title { get; init; } = string.Empty;

    public string TeachingContent { get; init; } = string.Empty;

    /// <summary>Exactly one Chapter per module has this set — the only
    /// one with a <see cref="PredictionPrompt"/>/<see cref="MiniPracticePrompt"/>
    /// and a cumulative <see cref="RecallPrompt"/>.</summary>
    public bool IsPrimaryWeight { get; init; }

    public string RecallPrompt { get; init; } = string.Empty;

    public bool IsCumulativeRecall { get; init; }

    /// <summary>Non-null ONLY on the <see cref="IsPrimaryWeight"/> chapter.</summary>
    public string? PredictionPrompt { get; init; }

    /// <summary>Non-null ONLY on the <see cref="IsPrimaryWeight"/> chapter.</summary>
    public string? MiniPracticePrompt { get; init; }
}

/// <summary>
/// Evidence produced by the learner — the Runtime's central asset (per
/// explicit instruction 2026-07-14: this, not video, is the heart of the
/// system, the same way <c>ModuleScript</c>/<c>MetricVerification</c>
/// turned out to be Studio's heart).
/// </summary>
/// <remarks>
/// HARD RULE: this type represents PRODUCTION, never CONSUMPTION. A video
/// watched, a page viewed, a module marked "complete" by simply reaching
/// its end are NEVER represented as <see cref="StudentEvidence"/> — if it
/// wasn't authored/attempted/decided by the learner, it does not belong
/// here and must not be used to justify progression (see
/// <see cref="RuntimeStage.Completed"/>'s doc comment).
/// </remarks>
public sealed class StudentEvidence
{
    public Guid StudentEvidenceId { get; init; } = Guid.NewGuid();

    public Guid RuntimeSessionId { get; init; }

    /// <summary>The <c>CapabilityModule</c> (Studio-approved) this
    /// evidence was produced for.</summary>
    public Guid CapabilityModuleId { get; init; }

    /// <summary>Which cognitive act this evidence is proof of — never
    /// inferred, always set explicitly by the Runtime stage that captured
    /// it.</summary>
    public StudentEvidenceOrigin Origin { get; init; }

    /// <summary>One or more parts making up this evidence — deliberately
    /// a list from Paso 1 onward (see <see cref="StudentEvidencePart"/>),
    /// never a single hard-coded text field.</summary>
    public List<StudentEvidencePart> Parts { get; init; } = [];

    public EvidenceAssistanceLevel AssistanceLevel { get; init; }

    /// <summary>
    /// Must be <see langword="true"/> whenever <see cref="Origin"/> is
    /// <see cref="StudentEvidenceOrigin.Recall"/> or
    /// <see cref="StudentEvidenceOrigin.Prediction"/> — mirrors Studio's
    /// proven <c>RecallActivity.OccursBeforeInstruction</c>/
    /// <c>RecallVerification.OccursBeforeInstruction</c> pattern. Enforced
    /// by a future Runtime validator, not by this contract itself.
    /// </summary>
    public bool CapturedBeforeAssistance { get; init; }

    /// <summary>
    /// Optional link to an earlier <see cref="StudentEvidence"/> this one
    /// responds to or should be compared against — the primary use case is
    /// comparing a <see cref="StudentEvidenceOrigin.Reflection"/> or
    /// <see cref="StudentEvidenceOrigin.Production"/> entry against an
    /// earlier <see cref="StudentEvidenceOrigin.Prediction"/> from the
    /// same session (neuroscience principle P1 — prediction error).
    /// </summary>
    public Guid? ComparesToEvidenceId { get; init; }

    public DateTime CapturedDate { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// The Runtime's explicit, read-only projection of the pedagogical
/// contract Studio already approved for one module (fixed Paso 1,
/// 2026-07-14, per explicit instruction) — <see cref="TargetMetric"/>,
/// <see cref="RecallRequirement"/>, <see cref="LearnerProduction"/>,
/// <see cref="SuccessCriteria"/> mirror <c>ModuleSkeleton</c>'s
/// Arquitecto-approved fields exactly.
/// </summary>
/// <remarks>
/// This is the ONLY pedagogical authority the Runtime (and the Tutor
/// Agent operating within it) may consult for a given module. Neither the
/// Runtime nor the Tutor Agent may invent, reinterpret, or modify these
/// values — they are fixed once Studio's Gate 1/Gate 2 approve them.
/// Building/populating this projection from the persisted
/// <c>CapabilityModule</c> (and its <c>CapabilityModuleMetric</c> rows) is
/// a later Paso; this type only defines the shape.
/// </remarks>
public sealed class RuntimePedagogicalContract
{
    public Guid CapabilityModuleId { get; init; }

    /// <summary>The parent Capability's id (fixed 2026-07-16) — lets an
    /// API caller fetch the full course structure (GET
    /// /capabilities/{id}/content) to render a course-wide sidebar,
    /// without the Runtime itself needing to know about levels/other
    /// modules.</summary>
    public Guid CapabilityId { get; init; }

    public CapabilityMetric TargetMetric { get; init; }

    public string RecallRequirement { get; init; } = string.Empty;

    public string LearnerProduction { get; init; } = string.Empty;

    /// <summary>The Instructor's CONCRETE task instructions (fixed
    /// 2026-07-17 — closes a real grounding gap: the Tutor Agent was
    /// inventing the concrete exercise content — e.g. "las cinco
    /// expresiones" — fresh every LearnerProduction turn, with zero
    /// stored grounding, since only the terse <see cref="LearnerProduction"/>
    /// description existed before. When this is a numbered list (multiple
    /// discrete items), the Runtime presents ONE item at a time via
    /// <c>MultiPartPromptSegmenter</c> — same mechanism as a chapter's
    /// PredictionPrompt. Empty for modules published before this fix.</summary>
    public string LearnerTask { get; init; } = string.Empty;

    public IReadOnlyList<string> SuccessCriteria { get; init; } = [];

    /// <summary>The module's title (fixed 2026-07-16) — used by the Tutor
    /// Agent to write a genuine, grounded <see cref="RuntimeStage.ModuleStarted"/>
    /// introduction instead of jumping straight into an unassisted Recall
    /// attempt with no context (see that stage's doc comment).</summary>
    public string ModuleTitle { get; init; } = string.Empty;

    /// <summary>The module's short description (fixed 2026-07-16) — same
    /// rationale as <see cref="ModuleTitle"/>.</summary>
    public string ModuleDescription { get; init; } = string.Empty;

    /// <summary>The parent Capability's display name (fixed 2026-07-16) —
    /// carried here purely for API/UI header display (e.g. "Preparar café
    /// en prensa francesa correctamente"), never used as pedagogical
    /// content by the Tutor Agent.</summary>
    public string CapabilityTitle { get; init; } = string.Empty;

    /// <summary>The parent Capability's stable code (fixed 2026-07-16) —
    /// same UI-only rationale as <see cref="CapabilityTitle"/>.</summary>
    public string CapabilityCode { get; init; } = string.Empty;

    /// <summary>The module's instructional content (fixed Paso 9,
    /// 2026-07-15) — projected straight from <c>CapabilityModule.Script</c>.
    /// Only ever surfaced to the Tutor/learner during
    /// <see cref="RuntimeStage.Instruction"/> (see <c>TutorTurnContextBuilder</c>);
    /// carried here so no Runtime executor needs its own database
    /// dependency to present it. Used ONLY as a legacy fallback when
    /// <see cref="Chapters"/> is empty (fixed 2026-07-16).</summary>
    public string ModuleScript { get; init; } = string.Empty;

    /// <summary>Ordered, phase-based teaching segments (fixed 2026-07-16)
    /// — when non-empty, the Runtime presents these ONE AT A TIME
    /// (<see cref="RuntimeStage.ChapterTeaching"/> et al.) instead of the
    /// single whole-script <see cref="RuntimeStage.Instruction"/> turn.
    /// Empty for modules published before this feature (legacy fallback
    /// to <see cref="ModuleScript"/>).</summary>
    public IReadOnlyList<RuntimeModuleChapter> Chapters { get; init; } = [];

    /// <summary>The Macro-Cycle's single closing reflection (fixed
    /// 2026-07-16) — mirrors Studio's <c>ModuleScript.ReflectionPrompt</c>
    /// exactly. Presented during <see cref="RuntimeStage.Reflection"/>
    /// instead of a generic Tutor-authored reflection question, when
    /// non-empty.</summary>
    public string ReflectionPrompt { get; init; } = string.Empty;
}

/// <summary>
/// A single learner's live session through one module's Runtime state
/// machine (fixed Paso 1, 2026-07-14). The Tutor Agent (Paso 4+) operates
/// WITHIN a <see cref="RuntimeSession"/>; it never owns or mutates
/// <see cref="Stage"/> directly (see DECISIÓN 1 in
/// /memories/repo/humanstudio-multiagent-vision.md).
/// </summary>
public sealed class RuntimeSession
{
    public Guid RuntimeSessionId { get; init; } = Guid.NewGuid();

    public Guid PersonId { get; init; }

    public Guid CapabilityModuleId { get; init; }

    /// <summary>The fixed, Studio-approved pedagogical contract this
    /// entire session is grounded in — never re-derived mid-session.</summary>
    public RuntimePedagogicalContract Contract { get; init; } = null!;

    /// <summary>Owned exclusively by the Runtime (code), never by the
    /// Tutor Agent's own judgment.</summary>
    public RuntimeStage Stage { get; set; } = RuntimeStage.ModuleStarted;

    /// <summary>All evidence produced so far in this session, in capture
    /// order — the only basis for progression, never time/consumption.</summary>
    public List<StudentEvidence> Evidence { get; init; } = [];

    public DateTime StartedDate { get; init; } = DateTime.UtcNow;

    public DateTime? CompletedDate { get; set; }
}
