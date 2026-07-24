namespace HumanOS.Contracts.People;

/// <summary>
/// Body for POST /api/onboarding/individual. Unlike
/// <see cref="CreateOnboardingRequest"/> (which also creates a company
/// Tenant), this creates ONLY a Person + PersonProfile with
/// <c>TenantId = null</c> — for an individual learner signing up on
/// Engram Academy without belonging to any organization. <c>Email</c> is
/// expected to be the address MSAL already returned for the signed-in
/// account, and the Azure OID/TID come from request headers, not this body.
/// </summary>
public sealed class CreateIndividualOnboardingRequest
{
    public string Email { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;
}
