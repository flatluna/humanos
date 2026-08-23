using Microsoft.Data.SqlTypes;

namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// Canonical, TENANT-WIDE entity catalog row (e.g. "Volunteer",
/// "i = √−1") — the "master file" for the Knowledge Graph feature
/// (2026-08-14): one row per distinct real-world concept, deduplicated by
/// (TenantId, NormalizedValue, EntityType) regardless of how many
/// documents/sections mention it. Per-occurrence evidence (which section,
/// what exact source text) lives on <see cref="StructuredLearningEntitySection"/>
/// instead — this row only ever holds the canonical definition.
/// Populated by <see cref="HumanOS.Services.StructuredLearningEntityExtractionService"/>,
/// run right after Step 2 (extraction) — never by Step 1/2 itself.
/// </summary>
public sealed class StructuredLearningEntity
{
    public Guid StructuredLearningEntityId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Domain this entity belongs to (same Subject catalog as
    /// <see cref="StructuredLearningEntityClass.SubjectId"/>) — lets the
    /// master registry be queried directly by (TenantId, SubjectId)
    /// instead of joining through occurrences/sections/documents. Null for
    /// entities extracted before this feature existed or when the owning
    /// document has no Subject/domain set.</summary>
    public Guid? SubjectId { get; set; }

    /// <summary>Knowledge Graph topic scoping (2026-08-16) — copied from the
    /// creating document's <see cref="StructuredLearningDocument.TopicId"/>
    /// at extraction time. Only actually used to SCOPE candidate matching
    /// for document-structural entity types (Article/LegalReference/
    /// Document/DocumentSection — see
    /// StructuredLearningEntityExtractionService.IsDocumentStructuralType)
    /// so e.g. "Artículo 1o." of different laws never collide just because
    /// both share the same broader SubjectId ("Leyes"). General concepts
    /// (Tax/PersonRole/LegalConcept/etc.) ignore this field and stay scoped
    /// by SubjectId alone, shared across every Topic in the domain.</summary>
    public Guid? TopicId { get; set; }

    /// <summary>Canonical display name — "Volunteer", "i = √−1", etc.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>"Person" / "PersonRole" / "Activity" / "MathematicalExpression"...
    /// Extensible — any string value is accepted.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Master de Clases (2026-08-14, Fase 2) — resolved catalog
    /// class for <see cref="EntityType"/>, scoped to this entity's own
    /// domain (Tenant+Subject). Null for entities extracted before this
    /// feature existed, or when the document has no SubjectId set.</summary>
    public Guid? EntityClassId { get; set; }

    /// <summary>Short canonical definition of this entity, if known.</summary>
    public string? Description { get; set; }

    /// <summary>Role ontology (2026-08-16) — for entities that represent a
    /// ROLE (e.g. "Contribuyente", "Retenedor") rather than a class-of-being
    /// itself, the name of the underlying class-of-being that plays this
    /// role (e.g. "Persona" for "Contribuyente" — a real person/organization
    /// can hold many roles at once, so the role name alone never explains
    /// what actually plays it). Null for entities that ARE a class-of-being
    /// already (Person, Organization, Animal, RealEstate...) or for legacy
    /// rows extracted before this field existed.</summary>
    public string? OntologicalClass { get; set; }

    /// <summary>Alternate surface forms / synonyms that should resolve to
    /// this same canonical entity (e.g. "volunteers", "i²=-1"), stored as
    /// a JSON string array.</summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>Lowercased/trimmed/whitespace-collapsed form of <see cref="Name"/>
    /// used for deduplication and exact-match lookups — never shown to a
    /// user, only compared against.</summary>
    public string NormalizedValue { get; set; } = string.Empty;

    /// <summary>Semantic embedding over Name + Aliases + Description, used
    /// as the fallback/complementary retrieval path when no exact/alias
    /// match is found — same embedding model as
    /// <see cref="StructuredLearningDocumentSection.Embedding"/>.</summary>
    public SqlVector<float>? Embedding { get; set; }

    public DateTime CreatedDate { get; set; }

    public ICollection<StructuredLearningEntitySection> Occurrences { get; set; } = [];
}
