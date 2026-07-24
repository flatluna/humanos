namespace HumanOS.Models.Capabilities;

/// <summary>
/// Subject/Field — the topical browsing axis for students (Finanzas,
/// Cocina, Recursos Humanos, Animales, Ciencia, Geografía, Matemáticas...).
/// Distinct from <see cref="CapabilityDomain"/> (Mind/Build/Home/Life/
/// Value/Future), which is Studio-only authoring metadata never shown to
/// students. See /memories/repo/student-graph-ui-redesign-final-design.md.
/// </summary>
public sealed class Subject
{
    public Guid SubjectId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; }

    public ICollection<Capability> Capabilities { get; set; } = [];

    public ICollection<SubjectTranslation> Translations { get; set; } = [];
}
