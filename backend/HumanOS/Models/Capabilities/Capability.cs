namespace HumanOS.Models.Capabilities;

using System.ComponentModel.DataAnnotations;
using HumanOS.Models.Capabilities.Graph;

public sealed class Capability
{
    public Guid CapabilityId { get; set; }

    public Guid CapabilityDomainId { get; set; }

    /// <summary>Topical browsing axis for students (Finanzas, Cocina,
    /// Ciencia...). Nullable — a capability may not be categorized yet.
    /// Distinct from <see cref="CapabilityDomainId"/>.</summary>
    public Guid? SubjectId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>The language this capability's ENTIRE generated content
    /// (graph node names/descriptions, blueprint step text, illustration
    /// prompts) was authored in — "es" or "en". Distinct from
    /// <see cref="Translations"/>, which only overlay static Name/
    /// Description labels for the catalog UI; a capability's real learning
    /// content (CapabilityGraphNode/NodeExperienceBlueprintStep rows) is
    /// authored ONCE, in exactly this language, and is never auto-
    /// translated. A topic like "Matemáticas"/"Math" exists as two
    /// SEPARATE Capability rows, one per language, each with its own full
    /// graph (2026-07-26 — bilingual course-creation feature). Defaults to
    /// "es" for backward compatibility with every capability created
    /// before this field existed.</summary>
    [MaxLength(10)]
    public string ContentLanguageCode { get; set; } = "es";

    /// <summary>Optional tone/persona directive for ALL generated learner-
    /// facing content on this capability (NodeExperienceBlueprint step text,
    /// and the live Tutor's per-turn phrasing). Null/empty (the default for
    /// every capability created before this field existed, and for every
    /// capability that never sets it) means "Standard" — zero prompt change
    /// vs. before this field existed. The only other supported value today
    /// is "YoungLearner" (K-12 early-childhood courses, e.g. a Grade 1
    /// phonics/reading course) — when set, ExperienceDesignerAgent and
    /// GraphNodeAdditionAgent append a warm/playful/simple-words,
    /// direct-to-the-child persona block to their prompts, WITHOUT changing
    /// the fixed 5-step Hypothesis/Teaching/Recall/Production/Assessment
    /// structure every other part of Runtime/Tutor/Assessment assumes.
    /// Deliberately a free-form string (not yet an enum) since this is a new,
    /// still-evolving dimension — see /memories/repo (K-12 lesson course
    /// feature, 2026-07-26).</summary>
    [MaxLength(30)]
    public string? PedagogicalAudience { get; set; }

    /// <summary>
    /// Orthogonal to <see cref="PedagogicalAudience"/> (that's TONE; this is
    /// STRUCTURE). Default false — zero behavior change for every capability
    /// created before this field existed and every traditional/continuing-
    /// education capability (cooking, carpentry, certifications, Algebra,
    /// etc.). When true (formal/official K-12-style school-track
    /// capabilities), every node's NodeExperienceBlueprint gains an OPTIONAL
    /// 6th step, <see cref="Graph.ExperienceStepType.DeepDive"/>, inserted
    /// between Teaching and Recall: 3+ short "example cards" illustrated
    /// with cheap emoji (never a generated image) — see
    /// <see cref="Graph.ExampleCard"/>. Never changes the fixed 5-step
    /// Hypothesis/Teaching/Recall/Production/Assessment order or content
    /// rules (2026-07-29 feature).
    /// </summary>
    public bool IncludeExamplesStep { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public CapabilityDomain CapabilityDomain { get; set; } = null!;

    public Subject? Subject { get; set; }

    public ICollection<CapabilityTranslation> Translations { get; set; } = [];

    public ICollection<PersonCapability> PersonCapabilities { get; set; } = [];

    public ICollection<CapabilityLevel> Levels { get; set; } = [];

    public ICollection<CapabilityKnowledgeChunk> KnowledgeChunks { get; set; } = [];

    /// <summary>
    /// Capability Graph para esta Capability (relación 1:1, opcional en PASO 1).
    /// En PASO 2, GraphArchitectAgent poblará estos grafos automáticamente.
    /// </summary>
    public CapabilityGraph? CapabilityGraph { get; set; }
}
