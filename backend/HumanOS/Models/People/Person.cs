using HumanOS.Models.Tenancy;

namespace HumanOS.Models.People;

public sealed class Person
{
    public Guid PersonId { get; set; }

    public Guid TenantId { get; set; }

    public string AzureOid { get; set; } = null!;

    public string AzureTid { get; set; } = null!;

    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation property
     */

    public Tenant Tenant { get; set; } = null!;
}
