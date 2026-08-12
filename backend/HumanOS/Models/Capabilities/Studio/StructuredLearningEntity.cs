namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// A real-world object/actor mentioned in the document (e.g. "Volunteer",
/// "Participant") — the "nodes" of a lightweight domain ontology, scoped
/// per document (not deduplicated across a tenant's other documents yet).
/// Schema exists from Step 1 on, but is only POPULATED by a future,
/// separate LLM-based step — Step 1 itself never writes to this table.
/// </summary>
public sealed class StructuredLearningEntity
{
    public Guid StructuredLearningEntityId { get; set; }

    public Guid StructuredLearningDocumentId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>"Voluntario", "Participante", etc.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>"Person" / "Role" / "Concept" / "Rule" / "Organization"...</summary>
    public string EntityType { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Section where this entity is first defined/mentioned —
    /// provenance back to the source text.</summary>
    public Guid? PrimarySectionId { get; set; }

    public DateTime CreatedDate { get; set; }

    public StructuredLearningDocument Document { get; set; } = null!;

    public StructuredLearningDocumentSection? PrimarySection { get; set; }
}
