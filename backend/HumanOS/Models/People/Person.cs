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

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation property
     */

    public Tenant? Tenant { get; set; }
}
