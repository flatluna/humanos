namespace HumanOS.Agents.Studio;

using System.Text.Json.Serialization;

/// <summary>
/// The 6 fixed Human Evolution Layers every Capability's levels map to
/// (see /memories/repo/human-os-core-philosophy.md). Every Capability has
/// its own instance of all 6 levels — a person's GLOBAL evolution layer is
/// a separate, computed/emergent concept aggregated across capabilities
/// (Hybrid Model / "Option C"), not modeled here.
/// </summary>
/// <remarks>
/// FRAMEWORK RULE (fijada Paso 1, 2026-07-14 — ver
/// <c>HUMAN-OS-STUDIO.md</c> §10): actualmente el agente Arquitecto solo
/// puede seleccionar <see cref="Foundation"/>, <see cref="Exploration"/> y
/// <see cref="Mastery"/> al diseñar un blueprint. <see cref="Professional"/>,
/// <see cref="Frontier"/> y <see cref="Creator"/> se conservan en el enum
/// únicamente para evitar migraciones de datos innecesarias — NO forman
/// parte del flujo activo todavía. No eliminar estos valores; no
/// habilitarlos sin una decisión explícita que actualice esta regla.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HumanEvolutionLayer
{
    Foundation,
    Exploration,
    Mastery,

    /// <summary>Reservado — no seleccionable por el Arquitecto todavía (Paso 1).</summary>
    Professional,

    /// <summary>Reservado — no seleccionable por el Arquitecto todavía (Paso 1).</summary>
    Frontier,

    /// <summary>Reservado — no seleccionable por el Arquitecto todavía (Paso 1).</summary>
    Creator
}

/// <summary>
/// The 7 metrics a module can raise.
/// </summary>
/// <remarks>
/// NAMING RULE (fijada Paso 1, 2026-07-14): <see cref="HumanEvolutionLayer.Mastery"/>
/// se usa ÚNICAMENTE como nivel; <see cref="Fluency"/> se usa ÚNICAMENTE
/// como métrica. Nunca renombrar/confundir uno con el otro en código,
/// prompts o UI.
/// <para>
/// MVP METRIC SCOPE (fijada 2026-07-14, ver <c>HUMAN-OS-STUDIO.md</c> §16):
/// el Arquitecto solo puede seleccionar actualmente <see cref="Recall"/>,
/// <see cref="Application"/>, <see cref="Confidence"/> e
/// <see cref="Independence"/> — el subconjunto mínimo que prueba la tesis
/// del "Memory Paradox" (internalización vs. dependencia de la IA) sin
/// necesitar medición longitudinal. <see cref="Knowledge"/>,
/// <see cref="Retention"/> y <see cref="Fluency"/> se conservan en el enum
/// (para no forzar migraciones) pero quedan FUERA del flujo activo hasta
/// una decisión explícita que actualice esta regla —
/// <see cref="BlueprintValidator.ActiveMetrics"/> rechaza cualquier
/// blueprint que use una métrica fuera de este subconjunto.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CapabilityMetric
{
    /// <summary>Reservada — no seleccionable por el Arquitecto en el MVP actual.</summary>
    Knowledge,

    Recall,

    Application,

    Independence,

    Confidence,

    /// <summary>Reservada — no seleccionable por el Arquitecto en el MVP actual.</summary>
    Retention,

    /// <summary>Reservada — no seleccionable por el Arquitecto en el MVP actual.</summary>
    Fluency
}

/// <summary>
/// Conceptual states for a module's <c>RecallMechanism</c> — DISTINCT from
/// <see cref="CapabilityMetric.Recall"/> as a <c>TargetMetric</c>. Defined
/// in Paso 1 (2026-07-14, see <c>HUMAN-OS-STUDIO.md</c> §10) as a concept
/// only — not yet wired into <c>ModuleSkeleton</c>/<c>ModuleScript</c> or
/// evaluated by any agent. Purpose: prevent conflating "the module
/// mentions memory somewhere" with "the module has a real, unassisted
/// retrieval moment" (RecallMechanism) OR with "Recall is this module's
/// primary verified skill" (TargetMetric == Recall) — these are two
/// independent concepts:
/// <list type="bullet">
/// <item><description><c>RecallMechanism</c> = an explicit, unassisted
/// retrieval moment required inside ANY module, regardless of its
/// TargetMetric (e.g. TargetMetric=Application can still require the
/// learner to first recall criteria from memory before applying them).</description></item>
/// <item><description><c>TargetMetric</c> = the single primary capability
/// the module is designed to verify (one of the 7 <see cref="CapabilityMetric"/>
/// values). TargetMetric == Recall is reserved for modules whose explicit
/// goal is verifying unassisted retrieval itself — it is never assigned
/// automatically just because a RecallMechanism is present.</description></item>
/// </list>
/// </summary>
public enum RecallMechanismStatus
{
    /// <summary>No existe recuperación no asistida en el módulo.</summary>
    Missing,

    /// <summary>El alumno recupera usando palabras clave o pistas.</summary>
    WithCues,

    /// <summary>El alumno recupera sin materiales, pistas ni IA.</summary>
    WithoutCues
}

/// <summary>
/// The 2 operational states an <c>InstructorAgent</c>-declared
/// <c>RecallActivity</c> can have (fixed Paso 3, 2026-07-14 — see
/// <c>HUMAN-OS-STUDIO.md</c> §12). Unlike <see cref="RecallMechanismStatus"/>
/// (Paso 1's still-unused conceptual 3-state definition, which includes
/// <c>Missing</c>), this one deliberately has only 2 values: by the time
/// the Instructor writes a script, Paso 2's <c>BlueprintValidator</c> has
/// already guaranteed the module HAS a <c>RecallRequirement</c> — "missing"
/// is no longer a valid state here, only whether cues were given.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecallSupportLevel
{
    /// <summary>El alumno recupera usando palabras clave o pistas.</summary>
    WithCues,

    /// <summary>El alumno recupera sin materiales, pistas ni IA.</summary>
    WithoutCues
}

/// <summary>
/// Per-call token usage for a single agent invocation (fixed Paso 3,
/// 2026-07-14 — see <c>HUMAN-OS-STUDIO.md</c> §12). Secondary/observability
/// concern only: counts tokens as reported by Azure OpenAI via
/// <c>AgentResponse.Usage</c> — does NOT compute monetary cost and does
/// NOT influence any pedagogical logic.
/// </summary>
public sealed class AgentTokenUsage
{
    public required string AgentName { get; init; }

    public string? ModuleId { get; init; }

    /// <summary>Azure OpenAI deployment name that actually served this call
    /// (e.g. "gpt4mini", "gpt-5-chat") — 2026-07-23, needed because
    /// different agents deliberately use different-cost models (economy
    /// vs. main tier, see CuradorAgent/DocumentContextAgent's doc
    /// comments), so a single flat cost-per-token rate is wrong. Empty for
    /// legacy usage that predates this field.</summary>
    public string ModelName { get; init; } = string.Empty;

    public int InputTokens { get; init; }

    public int OutputTokens { get; init; }

    /// <summary>
    /// How many of <see cref="InputTokens"/> were served from Azure
    /// OpenAI's automatic prompt cache (Paso 7, 2026-07-14 — diagnostic
    /// only, see HUMAN-OS-STUDIO.md §16). Already INCLUDED in
    /// <see cref="InputTokens"/> (per <c>UsageDetails.CachedInputTokenCount</c>'s
    /// own contract) — this field exists purely to observe whether/how
    /// much caching is already happening automatically (e.g. because the
    /// Instructor's long, identical system prompt repeats across every
    /// module call), not to change billing or pedagogical behavior.
    /// </summary>
    public int CachedInputTokens { get; init; }

    public int TotalTokens => InputTokens + OutputTokens;
}

/// <summary>
/// The processing states a single module moves through in the
/// Instructor/Métrico pipeline (fixed Paso 5, 2026-07-14 — see
/// <c>HUMAN-OS-STUDIO.md</c> §14). A module having "run through" the
/// Instructor and Métrico is NOT the same as being done — only
/// <see cref="Verified"/> counts as complete for the purpose of
/// assembling the final Capability.
/// </summary>
public enum ModuleProcessingStatus
{
    Pending,

    GeneratingScript,

    /// <summary>The Instructor finished, but the module is NOT verified
    /// yet — content exists, capability is not confirmed.</summary>
    ScriptGenerated,

    VerifyingMetric,

    /// <summary>Métrico found valid, evidence-backed proof of the
    /// approved TargetMetric — this is the ONLY status that counts toward
    /// "the capability is ready to assemble".</summary>
    Verified,

    /// <summary>The script exists and is structurally well-formed, but
    /// the evidence does not (yet) demonstrate the TargetMetric — a
    /// legitimate pedagogical outcome, not a technical error (e.g.
    /// Independence not verified because the learner received a
    /// checklist). Distinct from <see cref="Failed"/>.</summary>
    RequiresRevision,

    /// <summary>A technical error occurred, or an agent's structured
    /// output broke an already-established contract (e.g. TargetMetric
    /// changed between agents, Recall not before instruction) — a bug or
    /// inconsistency, not a pedagogical judgment call.</summary>
    Failed
}

/// <summary>
/// The 5 module types (4 original + Mentoria, added 2026-07-13) and the
/// metrics each one typically raises. The Métrico agent uses this as a
/// starting guide, not a hard rule.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModuleType
{
    Lectura,
    Video,
    Practica,
    SimuladorIA,
    Mentoria
}

/// <summary>The kind of raw material the Curador agent can ingest.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RawMaterialType
{
    Pdf,
    VideoTranscript,
    WebLink,
    UserNote,

    /// <summary>
    /// Findings automatically retrieved via Grounding with Bing Search
    /// (see <see cref="HumanOS.Agents.Studio.WebGroundingService"/>) to
    /// supplement/update a topic already present in the user's own
    /// material — never used to introduce topics the user's material
    /// didn't already cover. Each item's Content already carries inline
    /// source citations (title/URL/date) written by the grounding call
    /// itself, and Curador is instructed to judge their credibility rather
    /// than treat them as unconditionally true (see CuradorAgent
    /// Instructions).
    /// </summary>
    WebSearch
}

/// <summary>One piece of raw material fed into the factory by the user.</summary>
public sealed class RawMaterialItem
{
    public RawMaterialType Type { get; set; }

    /// <summary>Short human label (file name, URL, or note title).</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Plain text content, already extracted (e.g. via
    /// <see cref="HumanOS.Storage.PdfTextExtractor"/> for PDFs) before it
    /// reaches the Curador agent.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
