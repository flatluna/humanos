namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// One occurrence of a canonical <see cref="StructuredLearningEntity"/>
/// inside one <see cref="StructuredLearningDocumentSection"/> ("Engram") —
/// the many-to-many bridge (the "EngramEntity" of the Knowledge Graph
/// design) that preserves the EXACT verbatim source evidence for every
/// entity-to-engram connection. One Entity can have many rows here (one
/// per section it's mentioned in); one Section can have many rows here
/// (one per entity it mentions).
/// </summary>
public sealed class StructuredLearningEntitySection
{
    public Guid StructuredLearningEntitySectionId { get; set; }

    public Guid StructuredLearningEntityId { get; set; }

    public Guid StructuredLearningDocumentSectionId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Exact verbatim quote from the section's Text that this
    /// entity was extracted from — never paraphrased.</summary>
    public string SourceText { get; set; } = string.Empty;

    /// <summary>0-based character offset of <see cref="SourceText"/> within
    /// the section's Text, if it could be located.</summary>
    public int? StartPosition { get; set; }

    public int? EndPosition { get; set; }

    /// <summary>Extraction-time confidence/relevance, 0-1, if the
    /// extraction agent provided one.</summary>
    public double? RelevanceScore { get; set; }

    /// <summary>Master registry resolution outcome (2026-08-15, GraphRAG-style
    /// entity resolution) — "matched" (LLM reused an existing master entity),
    /// "new" (no reliable match, new entity proposed), or "ambiguous" (several
    /// plausible master entities, needs human review). Recorded per Engram
    /// occurrence — never overwritten by a later section's extraction.</summary>
    public string ResolutionStatus { get; set; } = "new";

    /// <summary>0-1 confidence the LLM gave for this entity's resolution
    /// against the master registry (only meaningful when <see cref="ResolutionStatus"/>
    /// is "matched" or "ambiguous"); null for legacy rows.</summary>
    public double? MatchConfidence { get; set; }

    /// <summary>Short LLM-provided justification for the resolution
    /// decision (e.g. "same canonical_label, alias and jurisdiction").</summary>
    public string? MatchReason { get; set; }

    public DateTime CreatedDate { get; set; }

    public StructuredLearningEntity Entity { get; set; } = null!;

    public StructuredLearningDocumentSection Section { get; set; } = null!;
}
