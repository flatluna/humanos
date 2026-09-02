namespace HumanOS.Models.OrgStructure;

using HumanOS.Models.Tenancy;

/// <summary>
/// A reusable, catalog-level reference entity for key responsibilities
/// that an organization authors once and can link across one or many
/// <see cref="JobRole"/>s — e.g. "Gestión de presupuestos anuales"
/// or "Liderazgo de equipo de desarrollo".
/// </summary>
public sealed class Responsibility
{
    public Guid ResponsibilityId { get; set; }

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

    public ICollection<JobRoleResponsibility> JobRoleResponsibilities { get; set; } = new List<JobRoleResponsibility>();
}
