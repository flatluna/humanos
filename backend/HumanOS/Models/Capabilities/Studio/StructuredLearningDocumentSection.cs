using Microsoft.Data.SqlTypes;

namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// One Chapter -&gt; Subchapter section (max 2 levels), fully extracted:
/// real verbatim text, size metrics, and a semantic embedding vector —
/// no LLM judgment involved in producing this row (see
/// <see cref="StructuredLearningDocument"/>).
/// </summary>
public sealed class StructuredLearningDocumentSection
{
    public Guid StructuredLearningDocumentSectionId { get; set; }

    public Guid StructuredLearningDocumentId { get; set; }

    /// <summary>Denormalized copy of the parent's TenantId — lets a
    /// semantic-search query filter/order directly on this table without
    /// a join.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Preserves the document's own chapter/section order.</summary>
    public int SortOrder { get; set; }

    public string ChapterTitle { get; set; } = string.Empty;

    /// <summary>One-sentence capability this engram is building inside the chapter.
    /// The chapter title acts as the schema/category bucket, while this field
    /// names the actual capability the learner is developing.</summary>
    public string Capability { get; set; } = string.Empty;

    public string SubchapterTitle { get; set; } = string.Empty;

    /// <summary>Full, verbatim text of this section — never paraphrased.</summary>
    public string Text { get; set; } = string.Empty;

    public int TotalCharacters { get; set; }

    /// <summary>Real token count (SharpToken, cl100k_base) — not the
    /// rough length/4 estimator used elsewhere for cost projection.</summary>
    public int TotalTokens { get; set; }

    public SqlVector<float> Embedding { get; set; }

    public int? PageFrom { get; set; }

    public int? PageTo { get; set; }

    /// <summary>Step 3 progress tracking — lets a resumed run skip sections
    /// already Completed instead of reprocessing (and re-paying for) them.</summary>
    public StructuredLearningPedagogyStatus PedagogyStatus { get; set; } = StructuredLearningPedagogyStatus.NotStarted;

    public string? PedagogyErrorMessage { get; set; }

    /// <summary>Prompt/contract version that produced this section's pedagogy.</summary>
    public string? PromptVersion { get; set; }

    /// <summary>Process version used for this section generation workflow.</summary>
    public string? ProcessVersion { get; set; }

    /// <summary>Model name used for this section's generation.</summary>
    public string? ModelName { get; set; }

    /// <summary>Hash or snapshot of the prompt/config for this specific section.</summary>
    public string? PromptHash { get; set; }

    /// <summary>JSON snapshot of the generation config used for this section.</summary>
    public string? ConfigSnapshotJson { get; set; }

    public DateTime CreatedDate { get; set; }

    /// <summary>JSON array (max 3 items) of the Recall questions generated
    /// once for this section/engram when the document's
    /// <see cref="StructuredLearningDocument.RecallGenerationMode"/> is
    /// Static — every student is served the same questions from here
    /// instead of generating new ones per attempt. Null when the document
    /// is Dynamic (the default/existing behavior).</summary>
    public string? StaticRecallItemsJson { get; set; }

    /// <summary>Same Static-mode question bank as
    /// <see cref="StaticRecallItemsJson"/>, but for the Assessment step
    /// (max 3 questions per engram).</summary>
    public string? StaticAssessmentQuestionsJson { get; set; }

    /// <summary>Knowledge Graph (2026-08-15) — the entity name (from this
    /// section's own extracted entities) the model chose as the conceptual
    /// hub/root of its relationship graph — used to render the tree
    /// starting from a single logical root instead of guessing via degree
    /// count. Null if the model found no clear hub for this section.</summary>
    public string? KnowledgeGraphRootEntityName { get; set; }

    /// <summary>Business-rule style statements inferred from this section before
    /// ontology generation. Stored as JSON in the same row to keep the schema
    /// backward-compatible while preserving a lightweight, section-scoped rule list.</summary>
    public List<StructuredLearningStatement> Statements { get; set; } = [];

    public StructuredLearningDocument Document { get; set; } = null!;

    public ICollection<StructuredLearningSectionStep> Steps { get; set; } = [];
}
