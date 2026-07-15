namespace HumanOS.Contracts.Capabilities;

public sealed class CapabilityDomainResponse
{
    public Guid CapabilityDomainId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }
}
