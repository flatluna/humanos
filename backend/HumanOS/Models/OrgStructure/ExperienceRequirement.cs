namespace HumanOS.Models.OrgStructure;

using HumanOS.Models.Tenancy;

/// <summary>
/// A reusable, catalog-level reference entity for required experiences
/// that an organization authors once and can link across one or many
/// <see cref="JobRole"/>s — e.g. "3+ years in B2B SaaS Sales"
/// or "5+ years in .NET and cloud architecture".
/// </summary>
public sealed class ExperienceRequirement
{
    public Guid ExperienceRequirementId { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Tenant Tenant { get; set; } = null!;

    public ICollection<JobRoleExperienceRequirement> JobRoleExperienceRequirements { get; set; } = new List<JobRoleExperienceRequirement>();
}
