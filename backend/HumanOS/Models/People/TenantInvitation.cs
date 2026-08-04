namespace HumanOS.Models.People;

public sealed class TenantInvitation
{
    public Guid TenantInvitationId { get; set; }

    public Guid TenantId { get; set; }

    public string Email { get; set; } = null!;

    public string NormalizedEmail { get; set; } = null!;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public Guid? DepartmentId { get; set; }

    public Guid? JobRoleId { get; set; }

    public Guid? ManagerId { get; set; }

    public PersonOrgRole OrgRole { get; set; } = PersonOrgRole.Employee;

    public TenantInvitationStatus Status { get; set; } = TenantInvitationStatus.Pending;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public DateTime? AcceptedDate { get; set; }

    public DateTime? RevokedDate { get; set; }
}
