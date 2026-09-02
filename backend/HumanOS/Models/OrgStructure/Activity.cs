namespace HumanOS.Models.OrgStructure;

using HumanOS.Models.Tenancy;

/// <summary>
/// A reusable, catalog-level reference entity for daily or periodic activities
/// that an organization authors once and can link across one or many
/// <see cref="JobRole"/>s — e.g. "Revisión diaria de métricas de ventas"
/// or "Planificación de sprint semanal".
/// </summary>
public sealed class Activity
{
    public Guid ActivityId { get; set; }

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

    public ICollection<JobRoleActivity> JobRoleActivities { get; set; } = new List<JobRoleActivity>();
}
