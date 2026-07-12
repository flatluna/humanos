namespace HumanOS.Models.Capabilities;

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
}
