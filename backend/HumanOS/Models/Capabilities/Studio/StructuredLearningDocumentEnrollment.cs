using HumanOS.Models.People;

namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// Explicit "Inscribirme" record for a Structured Learning document catalog
/// card — mirrors <see cref="HumanOS.Models.Capabilities.CapabilityEnrollment"/>'s
/// role for standalone Capabilities. Progress is not tracked here; this row
/// only records the enrollment itself so the document surfaces under "Mis
/// Engramas".
/// </summary>
public sealed class StructuredLearningDocumentEnrollment
{
    public Guid StructuredLearningDocumentEnrollmentId { get; set; } = Guid.NewGuid();

    public Guid PersonId { get; set; }

    public Guid StructuredLearningDocumentId { get; set; }

    public DateTime EnrolledDate { get; set; }

    public Person Person { get; set; } = null!;

    public StructuredLearningDocument Document { get; set; } = null!;
}
