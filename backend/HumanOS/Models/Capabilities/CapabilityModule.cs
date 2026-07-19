using HumanOS.Agents.Studio;

namespace HumanOS.Models.Capabilities;

public sealed class CapabilityModule
{
    public Guid CapabilityModuleId { get; set; }

    public Guid CapabilityLevelId { get; set; }

    public int SortOrder { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public ModuleType Type { get; set; }

    public string Script { get; set; } = null!;

    /// <summary>The Macro-Cycle's single closing reflection (fixed
    /// 2026-07-16 — see <see cref="HumanOS.Agents.Studio.ModuleScript.ReflectionPrompt"/>),
    /// comparing what the learner recalled/predicted against what they
    /// actually produced in <see cref="LearnerProduction"/>. Not yet
    /// consumed by the Interactive Learning Runtime.</summary>
    public string ReflectionPrompt { get; set; } = null!;

    public string MetricRationale { get; set; } = null!;

    /// <summary>Arquitecto-approved retrieval requirement (fixed Paso 4
    /// persistence gap, 2026-07-14 — see
    /// /memories/repo/human-os-runtime-design.md). Was previously only
    /// held in-memory as <c>ModuleSkeleton.RecallRequirement</c> during the
    /// Studio pipeline run and never persisted — the Interactive Learning
    /// Runtime needs this to build a <c>RuntimePedagogicalContract</c>
    /// from published data alone.</summary>
    public string RecallRequirement { get; set; } = null!;

    /// <summary>Arquitecto-approved observable learner production (fixed
    /// Paso 4 persistence gap, 2026-07-14) — same rationale as
    /// <see cref="RecallRequirement"/>.</summary>
    public string LearnerProduction { get; set; } = null!;

    /// <summary>The Instructor's CONCRETE task instructions (fixed
    /// 2026-07-17 — closes a real persistence gap: this was previously
    /// generated in-memory as <c>ModuleScript.LearnerTask</c> but NEVER
    /// persisted on its own, only folded into the whole <see cref="Script"/>
    /// narrative — so the Runtime's LearnerProduction stage had zero
    /// grounding in the actual concrete exercise content and the Tutor
    /// Agent was inventing it fresh, inconsistently, every turn). When the
    /// task naturally decomposes into several discrete items (e.g. "5
    /// expresiones"), the Instructor writes this as a numbered list so the
    /// Runtime can present one item at a time via
    /// <c>MultiPartPromptSegmenter</c> — same mechanism already used for
    /// a chapter's PredictionPrompt.</summary>
    public string LearnerTask { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    public CapabilityLevel CapabilityLevel { get; set; } = null!;

    public ICollection<CapabilityModuleMetric> Metrics { get; set; } = [];

    public ICollection<CapabilityModuleVerification> Verifications { get; set; } = [];

    public ICollection<CapabilityKnowledgeChunk> KnowledgeChunks { get; set; } = [];

    /// <summary>Ordered, short teaching-content segments for a future
    /// turn-based/voice presentation (fixed 2026-07-16) — see
    /// <see cref="CapabilityModuleChapter"/>. Not yet consumed by the
    /// Interactive Learning Runtime.</summary>
    public ICollection<CapabilityModuleChapter> Chapters { get; set; } = [];
}
