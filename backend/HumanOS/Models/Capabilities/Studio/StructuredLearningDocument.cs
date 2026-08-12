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

    /// <summary>Correlates back to the Studio wizard's in-progress RunId
    /// (same RunId pattern as CapabilityCreationRunStatus/TraditionalCourse
    /// run orchestrators).</summary>
    public Guid SessionId { get; set; }

    /// <summary>Original uploaded file name (use case 1: PDF upload).</summary>
    public string DocumentName { get; set; } = string.Empty;

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

    /// <summary>New (2026-08-10) — Graph/Traditional only ever generate
    /// MultipleChoice; OpenEnded is new here.</summary>
    public StructuredLearningAssessmentQuestionType AssessmentQuestionType { get; set; } = StructuredLearningAssessmentQuestionType.MultipleChoice;

    public int? AssessmentQuestionsPerRound { get; set; }

    public DateTime CreatedDate { get; set; }

    public ICollection<StructuredLearningDocumentSection> Sections { get; set; } = [];

    public ICollection<StructuredLearningEntity> Entities { get; set; } = [];

    public ICollection<StructuredLearningConnection> Connections { get; set; } = [];
}
