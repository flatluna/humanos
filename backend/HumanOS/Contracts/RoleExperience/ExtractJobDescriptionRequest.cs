namespace HumanOS.Contracts.RoleExperience;

/// <summary>Triggers extraction for a Job Description PDF already
/// uploaded via UploadRoleDocumentFunction (documentType:
/// "job-description").</summary>
public sealed class ExtractJobDescriptionRequest
{
    /// <summary>The storage path returned by the upload endpoint, e.g.
    /// "jobdescriptions/{personId}/{guid}-{fileName}".</summary>
    public string StoragePath { get; set; } = null!;

    public string FileName { get; set; } = null!;
}
