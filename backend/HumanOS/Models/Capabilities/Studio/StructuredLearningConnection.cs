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

    public Guid TargetEntityId { get; set; }

    /// <summary>Section where this connection was found/asserted —
    /// provenance back to the source text.</summary>
    public Guid? SourceSectionId { get; set; }

    /// <summary>Short verbatim quote/justification supporting this connection.</summary>
    public string? Evidence { get; set; }

    public DateTime CreatedDate { get; set; }

    public StructuredLearningDocument Document { get; set; } = null!;

    public StructuredLearningEntity SourceEntity { get; set; } = null!;

    public StructuredLearningEntity TargetEntity { get; set; } = null!;

    public StructuredLearningDocumentSection? SourceSection { get; set; }
}
