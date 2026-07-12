using EvidenceModel = HumanOS.Models.Evidence.Evidence;
using HumanOS.Models.People;

namespace HumanOS.Models.Capabilities;

public sealed class CapabilityEvidence
{
    public Guid CapabilityEvidenceId { get; set; }

    public Guid PersonCapabilityId { get; set; }

    public Guid EvidenceId { get; set; }

    public string EvidenceType { get; set; } = null!;

    public decimal? ContributionWeight { get; set; }

    public string ValidationStatus { get; set; } = "Pending";

    public Guid? ValidatedByPersonId { get; set; }

    public DateTime? ValidatedDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public PersonCapability PersonCapability { get; set; } = null!;

    public EvidenceModel Evidence { get; set; } = null!;

    public Person? ValidatedByPerson { get; set; }
}
