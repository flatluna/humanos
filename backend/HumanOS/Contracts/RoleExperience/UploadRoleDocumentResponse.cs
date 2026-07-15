namespace HumanOS.Contracts.RoleExperience;

public sealed class UploadRoleDocumentResponse
{
    public Guid PersonId { get; set; }

    public string DocumentType { get; set; } = null!;

    public string StoragePath { get; set; } = null!;

    public DateTime UploadedDate { get; set; }
}
