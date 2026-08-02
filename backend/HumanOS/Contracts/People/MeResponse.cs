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

    /// <summary>The signed-in Person's PersonProfile.PreferredLanguage
    /// ("en"/"es") — lets the frontend set its UI language from the
    /// account itself right after login, instead of only relying on
    /// browser/localStorage detection. Null if the profile doesn't exist
    /// yet (shouldn't normally happen once onboarded).</summary>
    public string? PreferredLanguage { get; set; }

    /// <summary>String name of PersonOrgRole ("Employee"/"Manager"/
    /// "HRAdmin") — lets the frontend gate the enterprise admin console
    /// (Departments/Job Roles) without a separate round-trip.</summary>
    public string OrgRole { get; set; } = "Employee";
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
