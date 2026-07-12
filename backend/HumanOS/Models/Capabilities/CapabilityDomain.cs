namespace HumanOS.Models.Capabilities;

public sealed class CapabilityDomain
{
    public Guid CapabilityDomainId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; }

    public ICollection<Capability> Capabilities { get; set; } = [];

    public ICollection<CapabilityDomainTranslation> Translations { get; set; } = [];
}
