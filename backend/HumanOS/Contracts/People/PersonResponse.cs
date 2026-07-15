namespace HumanOS.Contracts.People;

public sealed class PersonResponse
{
    public Guid PersonId { get; set; }

    public Guid TenantId { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
