namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// Knowledge Graph — Master de Clases de Arista (2026-08-15, Fase 3).
/// One row per REUSABLE relationship type within a domain (e.g. "Reformada
/// DOF", "trasladará", "se calculará aplicando la tasa de" for the "Leyes"
/// domain) — scoped by (TenantId, SubjectId), same convention as
/// <see cref="StructuredLearningEntityClass"/>. Deduplicated by
/// (TenantId, SubjectId, NormalizedLabel): the connection extraction agent
/// is given this catalog as context so it reuses an existing relationship
/// label instead of inventing near-duplicate phrasings for every document.
/// </summary>
public sealed class StructuredLearningRelationshipClass
{
    public Guid StructuredLearningRelationshipClassId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Domain this class belongs to (e.g. "Leyes") — same Subject
    /// catalog used by <see cref="StructuredLearningEntityClass.SubjectId"/>.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Reusable relationship phrase, e.g. "Reformada DOF", "trasladará".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>"Sequence"/"Requires"/"PartOf"/"Generic" — same coarse
    /// bucket as <see cref="StructuredLearningConnection.RelationshipCategory"/>.</summary>
    public string Category { get; set; } = "Generic";

    /// <summary>Generic, document-agnostic definition of what this
    /// relationship means as a category — e.g. "Indica que una disposición
    /// legal fue modificada por una reforma publicada en el DOF en cierta
    /// fecha" — never tied to one specific document's content.</summary>
    public string GenericDescription { get; set; } = string.Empty;

    /// <summary>Lowercased/trimmed/whitespace-collapsed form of <see cref="Label"/>
    /// used for deduplication — never shown to a user.</summary>
    public string NormalizedLabel { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
}
