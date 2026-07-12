using HumanOS.Models.Localization;

namespace HumanOS.Models.Capabilities;

public sealed class CapabilityDomainTranslation
{
    public Guid CapabilityDomainId { get; set; }

    public string LanguageCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public CapabilityDomain CapabilityDomain { get; set; } = null!;

    public Language Language { get; set; } = null!;
}
