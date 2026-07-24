namespace HumanOS.Contracts.People;

/// <summary>Response for GET /api/me — the real Tenant/Person a signed-in
/// Azure identity already onboarded into Human OS. TenantId/TenantName are
/// null for an individual (no-company) account.</summary>
public sealed class MeResponse
{
    public Guid PersonId { get; set; }

    public Guid? TenantId { get; set; }

    public string? TenantName { get; set; }

    public string? Email { get; set; }
}

/// <summary>Response for POST /api/onboarding or /api/onboarding/individual —
/// the newly created Person (+ Tenant, for the company flow), so the
/// frontend can refresh its session state. TenantId is null for an
/// individual (no-company) signup.</summary>
public sealed class OnboardingResponse
{
    public Guid PersonId { get; set; }

    public Guid? TenantId { get; set; }
}
