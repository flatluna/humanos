namespace HumanOS.Contracts.People;

/// <summary>
/// Body for POST /api/onboarding. The admin's real name and company
/// details are the only user-entered fields — <c>Email</c> is expected
/// to be the address MSAL already returned for the signed-in account
/// (the frontend fills it from the active MSAL account, not free text),
/// and the Azure OID/TID come from request headers, not this body.
/// </summary>
public sealed class CreateOnboardingRequest
{
    public string Email { get; set; } = null!;

    public string AdminFirstName { get; set; } = null!;

    public string AdminLastName { get; set; } = null!;

    public string CompanyName { get; set; } = null!;

    public string? CompanyAddress { get; set; }

    public string CompanyEmail { get; set; } = null!;

    public string? CompanyPhone { get; set; }
}
