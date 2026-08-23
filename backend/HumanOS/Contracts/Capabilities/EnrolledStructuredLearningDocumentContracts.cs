namespace HumanOS.Contracts.Capabilities;

/// <summary>
/// One Structured Learning document a person is enrolled in — built from an
/// explicit StructuredLearningDocumentEnrollment row. Mirrors
/// EnrolledCapabilityResponse's role for standalone Capabilities. Powers
/// "Mis Engramas".
/// </summary>
public sealed class EnrolledStructuredLearningDocumentResponse
{
    public Guid StructuredLearningDocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public int SectionCount { get; set; }
    public Guid? CoverIllustrationId { get; set; }
    public DateTime EnrolledDate { get; set; }
}

/// <summary>Response for POST/DELETE .../enroll on a Structured Learning
/// document.</summary>
public sealed class StructuredLearningDocumentEnrollmentActionResponse
{
    public Guid StructuredLearningDocumentId { get; set; }
    public bool Enrolled { get; set; }
    public DateTime? EnrolledDate { get; set; }
}
