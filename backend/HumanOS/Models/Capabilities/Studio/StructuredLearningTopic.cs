namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// Knowledge Graph — Catálogo de Tipos/Temas (2026-08-16). One row per
/// specific real-world "family" of documents WITHIN a domain (Subject) —
/// e.g. SubjectId="Leyes" can have many Topics: "Ley del IVA", "Ley del
/// ISR", "Ley Aduanera"; SubjectId="Matemáticas" can have "Álgebra
/// Booleana", "Cálculo"; SubjectId="Historia" can have "Aztecas", "Segunda
/// Guerra Mundial". One Subject -&gt; many Topics.
///
/// Used to scope master-registry entity resolution for document-structural
/// entity types (Article/LegalReference/Document/DocumentSection — things
/// like "Artículo 1o." that are NOT the same entity across different
/// document families) — see
/// StructuredLearningEntityExtractionService.IsDocumentStructuralType.
/// General concepts (Tax/PersonRole/LegalConcept/etc.) ignore Topic and
/// stay scoped by SubjectId alone, shared across every Topic in the domain.
/// </summary>
public sealed class StructuredLearningTopic
{
    public Guid StructuredLearningTopicId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>The domain this Topic belongs to — same Subject catalog as
    /// <see cref="StructuredLearningEntityClass.SubjectId"/>.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Short display name — e.g. "Ley del IVA".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Global description of this Topic, shown to authors when
    /// picking it for a new document.</summary>
    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }
}
