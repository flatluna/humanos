namespace HumanOS.Models.OrgStructure;

using HumanOS.Models.Tenancy;

/// <summary>
/// A reusable, catalog-level reference entity for professional certifications
/// that an organization authors once and can link across one or many
/// <see cref="JobRole"/>s — e.g. "AWS Certified Solutions Architect"
/// or "PMP - Project Management Professional".
/// </summary>
public sealed class Certification
{
    public Guid CertificationId { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Issuing organization or authority (e.g. "Microsoft", "AWS", "PMI", "Scrum Alliance", "Cisco").</summary>
    public string? Issuer { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Tenant Tenant { get; set; } = null!;

    public ICollection<JobRoleCertification> JobRoleCertifications { get; set; } = new List<JobRoleCertification>();
}
