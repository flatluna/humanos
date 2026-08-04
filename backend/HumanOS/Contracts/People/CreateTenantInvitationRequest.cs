namespace HumanOS.Contracts.People;

public sealed class CreateTenantInvitationRequest
{
    public string Email { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public Guid? DepartmentId { get; set; }

    public Guid? JobRoleId { get; set; }

    public Guid? ManagerId { get; set; }

    public string? OrgRole { get; set; }
}
