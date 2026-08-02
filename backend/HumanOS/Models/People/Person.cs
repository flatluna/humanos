using HumanOS.Models.OrgStructure;
using HumanOS.Models.Tenancy;

namespace HumanOS.Models.People;

public sealed class Person
{
    public Guid PersonId { get; set; }

    /// <summary>Null for an individual (no-company) account — see
    /// CreateIndividualOnboardingFunction. Non-null only for people who
    /// belong to an onboarded organization (CreateOnboardingFunction).</summary>
    public Guid? TenantId { get; set; }

    public string AzureOid { get; set; } = null!;

    public string AzureTid { get; set; } = null!;

    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginDate { get; set; }

    /// <summary>The Department this person belongs to in the tenant's org
    /// chart. Null until an HRAdmin assigns it (see
    /// UpdatePersonOrgAssignmentFunction) — always null for individual
    /// (no-tenant) accounts.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>The Job Role (puesto) this person currently holds. Null
    /// until assigned by an HRAdmin.</summary>
    public Guid? JobRoleId { get; set; }

    /// <summary>Self-referencing "reports to" pointer — the person's
    /// direct manager, another Person in the same Tenant. Null if
    /// unassigned or this person has no manager (e.g. a top-level exec).</summary>
    public Guid? ManagerId { get; set; }

    /// <summary>In-tenant permission tier for the enterprise admin console
    /// — see <see cref="PersonOrgRole"/>. Defaults to Employee for every
    /// account, including individual (no-tenant) ones.</summary>
    public PersonOrgRole OrgRole { get; set; } = PersonOrgRole.Employee;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Tenant? Tenant { get; set; }

    public Department? Department { get; set; }

    public JobRole? JobRole { get; set; }

    public Person? Manager { get; set; }

    public ICollection<Person> DirectReports { get; set; } = [];
}
