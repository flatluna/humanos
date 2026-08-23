namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// A relationship between two <see cref="StructuredLearningEntity"/> rows
/// (e.g. "Volunteer" --helps--&gt; "Participant") — the "edges" of a
/// lightweight domain ontology. Free-text <see cref="RelationshipLabel"/>
/// (not a fixed enum like GraphArchitectAgent's Requires/BuildsOn), since
/// this describes real-world domain relationships, not learning order.
/// Schema exists from Step 1 on, but is only POPULATED by a future,
/// separate LLM-based step — Step 1 itself never writes to this table.
/// </summary>
public sealed class StructuredLearningConnection
{
    public Guid StructuredLearningConnectionId { get; set; }

    public Guid StructuredLearningDocumentId { get; set; }

    public Guid TenantId { get; set; }

    public Guid SourceEntityId { get; set; }

    /// <summary>"ayuda a" / "lidera" / "requiere aprobación de", etc.</summary>
    public string RelationshipLabel { get; set; } = string.Empty;

    /// <summary>"Sequence"/"Requires"/"PartOf"/"Generic" — coarse bucket
    /// alongside the free-text <see cref="RelationshipLabel"/>, so queries
    /// can filter for e.g. ordered-checklist ("Sequence") edges without
    /// parsing natural language. See StructuredLearningConnectionExtractionAgent.</summary>
    public string RelationshipCategory { get; set; } = "Generic";

    /// <summary>Master de Clases de Arista (2026-08-15, Fase 3) — resolved
    /// catalog class for <see cref="RelationshipLabel"/>, scoped to this
    /// document's own domain (Tenant+Subject). Null for connections
    /// extracted before this feature existed, or when the document has no
    /// SubjectId set.</summary>
    public Guid? RelationshipClassId { get; set; }

    public Guid TargetEntityId { get; set; }

    /// <summary>Section where this connection was found/asserted —
    /// provenance back to the source text.</summary>
    public Guid? SourceSectionId { get; set; }

    /// <summary>Short verbatim quote/justification supporting this connection.</summary>
    public string? Evidence { get; set; }

    /// <summary>Epistemic origin of this relationship (2026-08-15,
    /// GraphRAG-style provenance) — "explicit" (directly stated by the
    /// text), "inferred" (reasonably deduced from the text), "referenced"
    /// (mentioned via an external reference whose own content was never
    /// verified/invented), "attributed" (reported as someone else's claim,
    /// not asserted directly), or "hypothetical" (a conditional/not-yet-
    /// triggered relationship, true only under a stated condition). Never
    /// "referenced" content should be treated as if the referenced
    /// document's text was actually read.</summary>
    public string EpistemicStatus { get; set; } = "explicit";

    /// <summary>0-1 confidence the LLM gave this relationship.</summary>
    public double? Confidence { get; set; }

    public DateTime CreatedDate { get; set; }

    public StructuredLearningDocument Document { get; set; } = null!;

    public StructuredLearningEntity SourceEntity { get; set; } = null!;

    public StructuredLearningEntity TargetEntity { get; set; } = null!;

    public StructuredLearningDocumentSection? SourceSection { get; set; }
}
