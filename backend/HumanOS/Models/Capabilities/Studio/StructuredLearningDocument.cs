namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// One extraction session: a single uploaded/authored document, fully
/// split into <see cref="StructuredLearningDocumentSection"/> rows.
/// Step 1 of the new TOC-driven Studio pipeline (see
/// /memories/repo design notes) — deterministic extraction only, no LLM.
/// </summary>
public sealed class StructuredLearningDocument
{
    public Guid StructuredLearningDocumentId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Azure AD Object ID of the person who generated this document —
    /// same best-effort identity convention as Capability.OwnerPersonId's own
    /// doc comment. Null for older documents created before this field existed.</summary>
    public string? CreatedByOid { get; set; }

    /// <summary>Best-effort display email captured alongside CreatedByOid.</summary>
    public string? CreatedByEmail { get; set; }

    /// <summary>True when this document is available in the public student catalog.</summary>
    public bool IsGlobal { get; set; }

    /// <summary>True when the document is ready to be shown in a catalog.</summary>
    public bool IsPublished { get; set; }

    /// <summary>Correlates back to the Studio wizard's in-progress RunId
    /// (same RunId pattern as CapabilityCreationRunStatus/TraditionalCourse
    /// run orchestrators).</summary>
    public Guid SessionId { get; set; }

    /// <summary>Original uploaded file name (use case 1: PDF upload).</summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>Short document-wide orientation generated from the source material.</summary>
    public string? ExecutiveSummary { get; set; }

    // ---- Catalog/skill linking (2026-08-14) — same taxonomy as
    // Capability's own SubjectId/CapabilityDomainId/Category/target skill,
    // now settable up-front at Step 1 instead of only after generation. ----

    /// <summary>Topical browsing axis for students (Matemáticas, Ciencia...)
    /// — same catalog as <see cref="HumanOS.Models.Capabilities.Capability.SubjectId"/>.</summary>
    public Guid? SubjectId { get; set; }

    /// <summary>Knowledge Graph topic scoping (2026-08-16) — a specific
    /// real-world document family WITHIN SubjectId, from the
    /// <see cref="StructuredLearningTopic"/> catalog, e.g.
    /// SubjectId="Leyes" with Topic="Ley del IVA"/"Ley del ISR"/"Ley
    /// Aduanera". Documents that share the same TopicId are treated as the
    /// same real-world "family" for master-registry entity resolution
    /// purposes — see StructuredLearningEntityExtractionService's dual
    /// scoping rule (document-structural entities like "Artículo 1o." are
    /// scoped by Topic to avoid cross-law collisions; general concepts like
    /// "Contribuyente" stay scoped by SubjectId only, shared across all
    /// Topics in the domain). Null is valid — falls back to SubjectId-only
    /// scoping for every entity type, same as before this field existed.</summary>
    public Guid? TopicId { get; set; }

    /// <summary>Studio-only authoring taxonomy (Mind/Build/Home/Life/Value/
    /// Future) — same catalog as <see cref="HumanOS.Models.Capabilities.Capability.CapabilityDomainId"/>.</summary>
    public Guid? CapabilityDomainId { get; set; }

    /// <summary>Employer career-plan track this document belongs to
    /// (job-skills/soft-skills/department-processes/hr-processes), or null
    /// for a personal/consumer document — same convention as
    /// <see cref="HumanOS.Models.Capabilities.Capability.Category"/>.</summary>
    public string? Category { get; set; }

    /// <summary>Skill/requirement (from the shared Skill catalog, or a new
    /// free-typed name) this document is meant to cover — only meaningful
    /// when <see cref="Category"/> is set.</summary>
    public string? TargetSkill { get; set; }

    // ---- Knowledge Graph opt-in (2026-08-14) — the designer decides at
    // Step 1 whether either phase runs at all; both default true (existing
    // behavior unchanged for anyone who never touches these toggles). ----

    /// <summary>Phase 1: extract domain entities ("nodes") per section
    /// (<see cref="HumanOS.Services.StructuredLearningEntityExtractionService"/>).
    /// If false, Phase 2 (connections) is skipped too regardless of its own
    /// flag, since it has nothing to connect.</summary>
    public bool IncludeKnowledgeGraphEntities { get; set; } = true;

    /// <summary>Phase 2: extract relationships ("edges") between entities
    /// already linked to the same section
    /// (<see cref="HumanOS.Services.StructuredLearningConnectionExtractionService"/>).</summary>
    public bool IncludeKnowledgeGraphConnections { get; set; } = true;

    // ---- Step 3 pedagogical configuration (2026-08-10) — controls HOW the
    // future "Diseñador de Sección" agent writes each section's steps.
    // Same field names as Capability's equivalent settings where a direct
    // equivalent exists, for consistency. ----

    /// <summary>"Standard" (default) or "YoungLearner" — same convention
    /// as <see cref="HumanOS.Models.Capabilities.Capability.PedagogicalAudience"/>.</summary>
    public string? PedagogicalAudience { get; set; }

    /// <summary>Version of the prompt/contract under which this document's
    /// structured-learning run was generated.</summary>
    public string? PromptVersion { get; set; }

    /// <summary>Version of this overall generation process (e.g. "structured-learning-pedagogy-v1").</summary>
    public string? ProcessVersion { get; set; }

    /// <summary>Model name used for the LLM generation behind this run.</summary>
    public string? ModelName { get; set; }

    /// <summary>Hash or snapshot of the prompt/config that produced this run.</summary>
    public string? PromptHash { get; set; }

    /// <summary>JSON snapshot of the generation config used for this run.</summary>
    public string? ConfigSnapshotJson { get; set; }

    public int? MaxDeepDiveCardsPerNode { get; set; }

    public bool IncludeProductionStep { get; set; } = true;

    /// <summary>New (2026-08-10) — Graph always has Recall on; this
    /// pipeline lets the designer turn it off entirely.</summary>
    public bool IncludeRecallStep { get; set; } = true;

    public int? RecallItemsRequired { get; set; }

    /// <summary>New (2026-08-22) — Graph always has Assessment on; this
    /// pipeline lets the designer turn it off entirely too (e.g. courses
    /// for people with disabilities that don't need a graded quiz at the
    /// end of every section).</summary>
    public bool IncludeAssessmentStep { get; set; } = true;

    /// <summary>New (2026-08-10) — Graph/Traditional only ever generate
    /// MultipleChoice; OpenEnded is new here.</summary>
    public StructuredLearningAssessmentQuestionType AssessmentQuestionType { get; set; } = StructuredLearningAssessmentQuestionType.MultipleChoice;

    public int? AssessmentQuestionsPerRound { get; set; }

    public DateTime CreatedDate { get; set; }

    // ---- Document Type Detection Engine (2026-08-17, Paso 1) —
    // deterministic (no LLM/embeddings) structural classification, run once
    // at extraction time. See HumanOS.Services.DocumentClassification. ----

    /// <summary>The engine's recommended DocumentType (as its string name,
    /// e.g. "Law"/"Procedure"/"Unknown") — always set, even when Unknown.</summary>
    public string? DetectedDocumentType { get; set; }

    /// <summary>The winning classifier's score (0-1) — 0 when
    /// DetectedDocumentType is "Unknown".</summary>
    public double? DetectedDocumentTypeConfidence { get; set; }

    /// <summary>The designer's final choice after reviewing the
    /// recommendation in the wizard — null until confirmed. Mirrors
    /// DetectedDocumentType's string convention. Downstream
    /// consumers (chapter/engram extraction, pedagogy design) should read
    /// THIS field, never DetectedDocumentType directly, once set.</summary>
    public string? ConfirmedDocumentType { get; set; }

    /// <summary>True once the designer has explicitly accepted or
    /// overridden the recommendation (see ConfirmedDocumentType) —
    /// distinguishes "never reviewed" from "reviewed and happens to match
    /// the recommendation".</summary>
    public bool DocumentTypeConfirmedByUser { get; set; }

    public ICollection<StructuredLearningDocumentSection> Sections { get; set; } = [];

    public ICollection<StructuredLearningConnection> Connections { get; set; } = [];
}
