namespace HumanOS.Models.OrgStructure;

/// <summary>
/// Many-to-many junction: which <see cref="JobRole"/>s require a given <see cref="Certification"/>.
/// </summary>
public sealed class JobRoleCertification
{
    public Guid JobRoleId { get; set; }

    public JobRole JobRole { get; set; } = null!;

    public Guid CertificationId { get; set; }

    public Certification Certification { get; set; } = null!;

    public DateTime CreatedDate { get; set; }
}
