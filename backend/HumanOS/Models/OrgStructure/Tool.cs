namespace HumanOS.Models.OrgStructure;

using HumanOS.Models.Tenancy;

/// <summary>
/// A reusable, catalog-level reference entity for tools, software, or technologies
/// that an organization authors once and can link across one or many
/// <see cref="JobRole"/>s — e.g. "Microsoft Excel", "Salesforce",
/// "Docker", "Jira", "Figma".
/// </summary>
public sealed class Tool
{
    public Guid ToolId { get; set; }

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

    public ICollection<JobRoleTool> JobRoleTools { get; set; } = new List<JobRoleTool>();
}
