namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// Knowledge Graph — Master de Clases de Entidad (2026-08-14, Fase 1).
/// One row per REUSABLE entity type within a domain (e.g. "Article",
/// "Law", "PersonType" for the "Leyes" domain) — scoped by
/// (TenantId, SubjectId) so different domains (Leyes vs Matemáticas) never
/// share a catalog. Deduplicated by (TenantId, SubjectId, NormalizedName):
/// the extraction agent is given this catalog as context so it reuses an
/// existing class instead of inventing a new one for every document.
///
/// Deliberately NOT wired into <see cref="StructuredLearningEntity"/> yet —
/// this is Fase 1 only (the catalog itself); reusing it from extraction
/// is a later, separate step.
/// </summary>
public sealed class StructuredLearningEntityClass
{
    public Guid StructuredLearningEntityClassId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Domain this class belongs to (e.g. "Leyes") — reuses the
    /// same Subject catalog already shown in Step 1 of the wizard.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Short reusable code, e.g. "Article", "Law", "PersonType".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human-facing label, e.g. "Artículo".</summary>
    public string DisplayLabel { get; set; } = string.Empty;

    /// <summary>Generic, document-agnostic definition of this class — e.g.
    /// "Número y texto literal de una disposición legal, indicando la ley
    /// de la que forma parte" — never tied to one specific document's content.</summary>
    public string GenericDescription { get; set; } = string.Empty;

    /// <summary>Lowercased/trimmed/whitespace-collapsed form of <see cref="Name"/>
    /// used for deduplication — never shown to a user.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
}
