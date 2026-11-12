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

    /// <summary>Source-video timestamps (seconds) this Engram's clip spans,
    /// only populated for video-origin documents (null for PDF-origin
    /// documents) — lets Studio show the designer exactly what portion of
    /// the original video each Engram covers (2026-11-07).</summary>
    public double? VideoStartSeconds { get; set; }

    public double? VideoEndSeconds { get; set; }

    /// <summary>When true (2026-11-07), this Engram is purely informational
    /// (e.g. a course-orientation/welcome Engram) — pedagogy generation only
    /// produces its Teaching/Learn step, skipping Hypothesis/Recall/
    /// Production/Assessment entirely, since there's nothing to practice or
    /// be quizzed on. Currently only settable from the "New Learning From
    /// Video" flow (one checkbox per Engram), but the flag itself is not
    /// video-specific and any future authoring flow could set it too.
    /// Defaults false (existing behavior unchanged).</summary>
    public bool IsInformationalOnly { get; set; }

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

    /// <summary>How many times this section's pedagogy content has been
    /// (re)generated — starts at 0 (never generated), becomes 1 on the
    /// first successful generation, and increments by 1 every time it's
    /// regenerated (initial pedagogy run, "Continue generating", or
    /// "Restart engrams…" all funnel through the same update) so designers
    /// can tell an Engram apart from an older draft (2026-11-04).</summary>
    public int PedagogyVersion { get; set; }

    /// <summary>UTC timestamp of this section's most recent successful
    /// pedagogy (re)generation — shown next to <see cref="PedagogyVersion"/>
    /// in Studio so a designer can see exactly when an Engram was last
    /// regenerated (2026-11-04).</summary>
    public DateTime? PedagogyGeneratedAt { get; set; }

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
