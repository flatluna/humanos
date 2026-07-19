namespace HumanOS.Models.Capabilities;

using HumanOS.Models.Capabilities.Graph;

public sealed class Capability
{
    public Guid CapabilityId { get; set; }

    public Guid CapabilityDomainId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public CapabilityDomain CapabilityDomain { get; set; } = null!;

    public ICollection<CapabilityTranslation> Translations { get; set; } = [];

    public ICollection<PersonCapability> PersonCapabilities { get; set; } = [];

    public ICollection<CapabilityLevel> Levels { get; set; } = [];

    public ICollection<CapabilityKnowledgeChunk> KnowledgeChunks { get; set; } = [];

    /// <summary>
    /// Capability Graph para esta Capability (relación 1:1, opcional en PASO 1).
    /// En PASO 2, GraphArchitectAgent poblará estos grafos automáticamente.
    /// </summary>
    public CapabilityGraph? CapabilityGraph { get; set; }
}
