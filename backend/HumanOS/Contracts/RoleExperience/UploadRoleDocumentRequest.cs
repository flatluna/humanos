namespace HumanOS.Contracts.RoleExperience;

public sealed class UploadRoleDocumentRequest
{
    /// <summary>"job-description" or "resume".</summary>
    public string DocumentType { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public string ContentBase64 { get; set; } = null!;
}
