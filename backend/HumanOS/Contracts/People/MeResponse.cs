namespace HumanOS.Contracts.People;

/// <summary>Response for GET /api/me — the real Tenant/Person a signed-in
/// Azure identity already onboarded into Human OS.</summary>
public sealed class MeResponse
{
    public Guid PersonId { get; set; }

    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = null!;

    public string? Email { get; set; }
}

/// <summary>Response for POST /api/onboarding — the newly created
/// Tenant/Person, so the frontend can refresh its session state.</summary>
public sealed class OnboardingResponse
{
    public Guid PersonId { get; set; }

    public Guid TenantId { get; set; }
}
