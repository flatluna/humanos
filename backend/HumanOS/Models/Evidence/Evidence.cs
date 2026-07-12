using HumanOS.Models.Capabilities;
using HumanOS.Models.People;
using HumanOS.Models.Projects;

namespace HumanOS.Models.Evidence;

public sealed class Evidence
{
    public Guid EvidenceId { get; set; }

    public Guid PersonId { get; set; }

    public Guid CapabilityId { get; set; }

    public Guid? PersonProjectId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string EvidenceType { get; set; } = null!;

    public string? EvidenceUrl { get; set; }

    public string ValidationStatus { get; set; } = "Pending";

    public int AssistanceLevel { get; set; }

    public string? ValidationFeedback { get; set; }

    public DateTime? ValidatedDate { get; set; }

    public DateTime SubmittedDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public Person Person { get; set; } = null!;

    public Capability Capability { get; set; } = null!;

    public PersonProject? PersonProject { get; set; }
}
