namespace HumanOS.Contracts.People;

public sealed class TenantInvitationResponse
{
    public Guid TenantInvitationId { get; set; }

    public Guid TenantId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public Guid? DepartmentId { get; set; }

    public Guid? JobRoleId { get; set; }

    public Guid? ManagerId { get; set; }

    public string OrgRole { get; set; } = "Employee";

    public string Status { get; set; } = "Pending";

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public DateTime? AcceptedDate { get; set; }

    public DateTime? RevokedDate { get; set; }

    /// <summary>One-time temporary password for the newly created local account, only populated in the
    /// response of the CreateTenantInvitation call that created it \u2014 never persisted or returned again
    /// afterward. The HR admin must copy/share it with the employee out of band right away.</summary>
    public string? TemporaryPassword { get; set; }
}
