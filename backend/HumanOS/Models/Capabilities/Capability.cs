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
    /// STRUCTURE). Default true (2026-07-30) — every capability, regardless
    /// of subject/audience (cooking, carpentry, law, certifications, Algebra,
    /// K-12, etc.), gets a Deep Dive step; it proved valuable across every
    /// domain it was tried on, not just formal/school-track courses, so it
    /// is no longer an opt-in toggle. Every node's NodeExperienceBlueprint
    /// gains an OPTIONAL 6th step, <see cref="Graph.ExperienceStepType.DeepDive"/>,
    /// inserted between Teaching and Recall: a full coverage-pool set of
    /// short "example cards" illustrated with cheap emoji (never a generated
    /// image) — see <see cref="Graph.ExampleCard"/>. Never changes the fixed
    /// 5-step Hypothesis/Teaching/Recall/Production/Assessment order or
    /// content rules (2026-07-29 feature; default flipped 2026-07-30).
    /// </summary>
    public bool IncludeExamplesStep { get; set; } = true;

    /// <summary>
    /// Orthogonal to <see cref="IncludeExamplesStep"/> — this toggles the
    /// FIXED Memory Paradox's own Production ("Aplícalo"/"Aplicación")
    /// step, not the optional Deep Dive one (2026-08-04, client-facing
    /// course-configuration feature: some clients don't want an open-ended
    /// "apply it" step for every node). Default true (every capability
    /// created before this field existed keeps the Production step,
    /// zero behavior change). When false, every node's blueprint is
    /// designed/persisted/validated with only 4 steps — Hypothesis,
    /// Teaching, Recall, Assessment (plus the optional Deep Dive, if
    /// <see cref="IncludeExamplesStep"/> is also true) — and the Runtime
    /// simply never creates a Production LearningSessionStep for this
    /// capability's nodes.
    /// </summary>
    public bool IncludeProductionStep { get; set; } = true;

    /// <summary>
    /// How many DISTINCT Recall items a learner must master before the
    /// Recall step advances to Production/Assessment (2026-08-04, client-
    /// facing course-configuration feature — some clients find the default
    /// too demanding for every node). Null (the default, and every
    /// capability created before this field existed) means "use
    /// RecallLoopGate's own built-in default (3)". When set, must be
    /// between 1 and 10 — see RecallLoopGate.Evaluate's itemsRequiredToAdvance
    /// parameter.
    /// </summary>
    public int? RecallItemsRequired { get; set; }

    /// <summary>
    /// How many questions make up one Assessment ("cuestionario") round
    /// for this capability's nodes (2026-08-04, client-facing course-
    /// configuration feature — some clients find the default too long).
    /// Null (the default, and every capability created before this field
    /// existed) means "use AdaptiveAssessmentEngine's own built-in default
    /// (5)". When set, must be between 1 and 10.
    /// </summary>
    public int? AssessmentQuestionsPerRound { get; set; }

    /// <summary>Which of the employer-assigned career-plan training tracks
    /// this capability belongs to (2026-08-01 — "cursos de empresa"
    /// feature): "job-skills" | "soft-skills" | "department-processes" |
    /// "hr-processes", copied verbatim from the same 4 ids the career-plan
    /// frontends (human-os-employee/human-os-enterprise) already use as
    /// their tab/category ids. Null for capabilities not tied to any
    /// employer training track (e.g. personal/consumer courses).</summary>
    [MaxLength(30)]
    public string? Category { get; set; }

    /// <summary>When set (together with <see cref="TargetSkill"/>), HR
    /// explicitly authored this capability, in Capability Studio, to close
    /// ONE specific required skill/process gap on this JobRole — lets
    /// PersonCareerPlanService.AutoMatchCapabilitiesAsync link this
    /// capability's id directly onto that JobRole's roadmap step, instead
    /// of relying only on an exact Name/Title text match.</summary>
    public Guid? TargetJobRoleId { get; set; }

    /// <summary>The exact required-skill/process string (copied from the
    /// target JobRole's Technical/Soft/DepartmentProcesses/HrProcesses
    /// list) this capability is meant to satisfy. Null unless
    /// <see cref="TargetJobRoleId"/> is set.</summary>
    [MaxLength(400)]
    public string? TargetSkill { get; set; }

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
