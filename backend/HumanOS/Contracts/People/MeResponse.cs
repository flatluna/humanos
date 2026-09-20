namespace HumanOS.Contracts.People;

/// <summary>Response for GET /api/me — the real Tenant/Person a signed-in
/// Azure identity already onboarded into Human OS. TenantId/TenantName are
/// null for an individual (no-company) account.</summary>
public sealed class MeResponse
{
    public Guid PersonId { get; set; }

    public Guid? TenantId { get; set; }

    public string? TenantName { get; set; }

    /// <summary>True when the signed-in Person's Tenant is an academic
    /// institution (Tenant.OrgType == "Academic", e.g. Tecnológico de
    /// Monterrey) rather than a corporate employer — drives the employee
    /// portal's branding/presentation (logo, header copy, student-only
    /// sections). False (default) for a normal company tenant or an
    /// individual (no-company) account.</summary>
    public bool IsAcademicTenant { get; set; }

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

    /// <summary>Org chart Department/Job Role assignment for the
    /// signed-in Person (2026-08-02, "Mi Perfil" self-service page) —
    /// null until an HRAdmin assigns them. Lets the frontend show/derive
    /// "Ocupación" from the real assignment instead of free text, and
    /// fetch the full job description (GET /job-roles/{jobRoleId}) for a
    /// "my role & development" section.</summary>
    public Guid? DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    public Guid? JobRoleId { get; set; }

    public string? JobRoleTitle { get; set; }

    public Guid? ManagerId { get; set; }

    public string? ManagerDisplayName { get; set; }
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
