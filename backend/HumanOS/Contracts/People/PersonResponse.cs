namespace HumanOS.Contracts.People;

public sealed class PersonResponse
{
    public Guid PersonId { get; set; }

    public Guid? TenantId { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /// <summary>String name of PersonOrgRole ("Employee"/"Manager"/"HRAdmin").</summary>
    public string OrgRole { get; set; } = "Employee";

    /// <summary>Org chart Department assignment — see <see cref="HumanOS.Models.People.Person.DepartmentId"/>. Null until an HRAdmin assigns it.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Job Role ("puesto") assignment — see <see cref="HumanOS.Models.People.Person.JobRoleId"/>. Null until an HRAdmin assigns it.</summary>
    public Guid? JobRoleId { get; set; }
}
