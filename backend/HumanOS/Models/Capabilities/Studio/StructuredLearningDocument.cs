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

    /// <summary>Optional external code linking this document back to its origin
    /// (2026-10-25) — e.g. the Family app's AI capability recommendation code
    /// ("USA-PUBLIC-FIRST_GRADE-MATH-G1-MATH-07"). Lets a parent app find the
    /// Studio document it spawned without any cross-service callback, since
    /// both share the same database — see FamilyCapabilityService's
    /// studio-status sync. Null for documents not created from such a flow.</summary>
    public string? SourceCapabilityCode { get; set; }

    /// <summary>Short document-wide orientation generated from the source material.</summary>
    public string? ExecutiveSummary { get; set; }

    /// <summary>Azure AI Video Indexer VideoId this document was created from
    /// (2026-11-07) — null for PDF-sourced documents. Lets Studio's
    /// "Clases en Proceso" list tell a plain "indexed" video apart from one
    /// whose Engrams/course were actually generated, by matching against this
    /// field instead of relying on frontend-only (easily lost) state.</summary>
    public string? SourceVideoId { get; set; }

    // ---- Course origin metadata (2026-11-09) — essential authorship/source
    // info the designer fills in at Step 1 of "New Learning From Video"
    // (and, going forward, any new document). Orthogonal to the catalog
    // fields below (SubjectId/Category/TargetSkill etc. classify the course
    // for students browsing it; these describe who actually created/
    // presented the source content and in what language the course TEXT
    // itself should be generated). ----

    /// <summary>"SoftSkill" or "TechnicalSkill" — the designer's own
    /// classification of the course, distinct from <see cref="Category"/>/
    /// <see cref="TargetSkill"/> (which drive catalog browsing). Null for
    /// documents created before this field existed.</summary>
    public string? SkillType { get; set; }

    /// <summary>Name of the person who authored/presented the source
    /// material (e.g. the professor in the video), as free text.</summary>
    public string? AuthorName { get; set; }

    /// <summary>Course name as given by the author/presenter — distinct
    /// from <see cref="DocumentName"/> (the Studio-internal document title,
    /// which may be edited/defaulted independently).</summary>
    public string? AuthorCourseName { get; set; }

    /// <summary>Date the source video/lecture was actually presented/
    /// recorded (not the date the Structured Learning document was
    /// created — see <see cref="CreatedDate"/> for that).</summary>
    public DateTime? PresentedDate { get; set; }

    /// <summary>Kind of entity where the source content was presented —
    /// e.g. "Universidad"/"Salon de clases"/"Empresa"/"Conferencia"/
    /// "Online"/"Otro" (combo box in the UI, also accepts free text).</summary>
    public string? PresentedEntityType { get; set; }

    /// <summary>Specific name of the entity from <see cref="PresentedEntityType"/>
    /// (e.g. "MIT", "Acme Corp") — free text, optional.</summary>
    public string? PresentedEntityName { get; set; }

    /// <summary>Language the course TEXT/pedagogy should be generated in
    /// (e.g. "es"/"en") — set once at course creation so Step 3 (pedagogy)
    /// never has to silently default to Spanish. Distinct from any video
    /// transcription language, which is a separate upstream concern (Azure
    /// AI Video Indexer's own language setting).</summary>
    public string? CourseLanguageCode { get; set; }

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

    /// <summary>Whether the "Knowledge Graph"/"Knowledge Facts" viewer is
    /// exposed to STUDENTS while taking the course (2026-08-23) — separate
    /// from <see cref="IncludeKnowledgeGraphEntities"/>/
    /// <see cref="IncludeKnowledgeGraphConnections"/>, which only control
    /// whether the graph is generated at all for authoring purposes. An
    /// instructor may still want the graph generated (for their own review)
    /// without showing it to learners during real-mode training. Defaults
    /// true so existing behavior (always shown) is unchanged.</summary>
    public bool ShowKnowledgeGraphToLearners { get; set; } = true;

    // ---- Step 3 pedagogical configuration (2026-08-10) — controls HOW the
    // future "Diseñador de Sección" agent writes each section's steps.
    // Same field names as Capability's equivalent settings where a direct
    // equivalent exists, for consistency. ----

    /// <summary>"Standard" (default) or "YoungLearner" — same convention
    /// as <see cref="HumanOS.Models.Capabilities.Capability.PedagogicalAudience"/>.</summary>
    public string? PedagogicalAudience { get; set; }

    /// <summary>Content depth/seniority level (2026-09-18) — "L100"
    /// (foundational, assumes no prior knowledge), "L200" (assumes
    /// fundamentals, more worked examples/application), or "L300"
    /// (assumes L100/L200 mastery, focuses on trade-offs/edge cases/
    /// architecture). Null defaults to "L100" for backward compatibility
    /// with documents created before this field existed. Orthogonal to
    /// <see cref="PedagogicalAudience"/> (that's tone/age; this is depth).</summary>
    public string? CourseLevel { get; set; }

    /// <summary>Designer-controlled toggle (2026-10-30) for the Learn step's
    /// lecture illustrations/audio — independent of <see cref="PedagogicalAudience"/>.
    /// When true, the lecture is split into 3-6 ordered scenes, each with its
    /// own short audio clip + its own illustration (synced image/audio
    /// player), which is visually richer but generates up to 6 images + 6 TTS
    /// clips sequentially per section (much slower). When false, the Learn
    /// step generates ONE illustration + ONE full-lecture audio clip (fast).
    /// Defaults to true so documents created before this field existed keep
    /// their current (scened) behavior unchanged; new documents created via
    /// the Studio UI explicitly default this to false (single illustration).</summary>
    public bool UseScenedLecture { get; set; } = true;

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

    // ---- Learner-facing feature toggles + static/dynamic question
    // generation (2026-10-06) — set once at Step 3 of the Studio wizard
    // ("Course design"), some editable later from the course settings
    // screen. ----

    /// <summary>Whether the "Q&amp;A with the professor" trigger (voice or
    /// text — both share this single on/off switch) is offered to students
    /// during the Learn/Teaching step. Defaults true (existing behavior
    /// unchanged).</summary>
    public bool ShowQaProfessorStep { get; set; } = true;

    /// <summary>Whether "Speak with your agent about this example" (the
    /// voice tutor embedded in Deep Dive cards) is offered to students.
    /// When false, learners can still answer Deep Dive cards in writing.
    /// Defaults true (existing behavior unchanged).</summary>
    public bool ShowDeepDiveAgentChat { get; set; } = true;

    /// <summary>Designer cost lever (2026-10-06): when true (default,
    /// existing behavior), the QA Professor trigger offers BOTH text and
    /// real-time voice (student picks). When false, ONLY the cheap text
    /// chat is offered — the voice option is hidden entirely, saving the
    /// real-time voice session cost. Ignored when
    /// <see cref="ShowQaProfessorStep"/> is false (nothing is shown at
    /// all).</summary>
    public bool EnableQaProfessorVoice { get; set; } = true;

    /// <summary>Same cost lever as <see cref="EnableQaProfessorVoice"/>,
    /// applied to Deep Dive's "Speak with your agent about this example".
    /// When false, only the text chat is offered for Deep Dive cards (the
    /// typed-answer box is unaffected either way). Ignored when
    /// <see cref="ShowDeepDiveAgentChat"/> is false.</summary>
    public bool EnableDeepDiveAgentVoice { get; set; } = true;

    /// <summary>Whether the "In your role" workplace-application step is
    /// generated/shown at all for this course. Defaults true (existing
    /// behavior unchanged).</summary>
    public bool IncludeWorkplaceApplicationStep { get; set; } = true;

    /// <summary>Whether the "Hypothesis" (prediction prompt before
    /// Teaching) step is shown to students at all (2026-10-27) — lets a
    /// designer turn it off for pre-reader grades (Kindergarten/1st, where
    /// a written hypothesis prompt doesn't make pedagogical sense) without
    /// having to regenerate the course. Still always extracted/stored per
    /// section; this only controls learner-facing visibility, same
    /// convention as <see cref="IncludeWorkplaceApplicationStep"/>.
    /// Defaults true (existing behavior unchanged).</summary>
    public bool IncludeHypothesisStep { get; set; } = true;

    /// <summary>Static (fixed at course-creation time, same for every
    /// student, max 3 items per engram, cheaper) vs. Dynamic (generated on
    /// demand per student/attempt, more variety, costs an LLM call every
    /// time). Defaults Dynamic.</summary>
    public StructuredLearningGenerationMode RecallGenerationMode { get; set; } = StructuredLearningGenerationMode.Dynamic;

    /// <summary>Same Static/Dynamic choice as <see cref="RecallGenerationMode"/>,
    /// applied to the Assessment step's questions (max 3 per engram when
    /// Static). Defaults Dynamic.</summary>
    public StructuredLearningGenerationMode AssessmentGenerationMode { get; set; } = StructuredLearningGenerationMode.Dynamic;

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
