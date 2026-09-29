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
    /// <summary>Distinct sections this person has a Passed assessment round for.</summary>
    public int CompletedSectionCount { get; set; }
    public Guid? CoverIllustrationId { get; set; }
    public DateTime EnrolledDate { get; set; }
    /// <summary>"es"/"en" — see StructuredLearningLanguageHeuristic.</summary>
    public string Language { get; set; } = "es";
}

/// <summary>Response for POST/DELETE .../enroll on a Structured Learning
/// document.</summary>
public sealed class StructuredLearningDocumentEnrollmentActionResponse
{
    public Guid StructuredLearningDocumentId { get; set; }
    public bool Enrolled { get; set; }
    public DateTime? EnrolledDate { get; set; }
}
