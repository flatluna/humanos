namespace HumanOS.Models.People;

/// <summary>
/// Coarse in-tenant PERMISSION tier for the enterprise/org-structure admin
/// screens (Departments, Job Roles, and future Role↔Capability
/// requirements / career roadmap features). This is deliberately separate
/// from <see cref="HumanOS.Models.OrgStructure.JobRole"/> — that is the
/// person's job TITLE in the org chart ("Puesto"), this is what they're
/// ALLOWED TO MANAGE in the enterprise console. A person can be, e.g.,
/// JobRole "Ingeniero Senior" (title) while OrgRole is "Employee"
/// (permission), or JobRole "Directora de RRHH" while OrgRole is
/// "HRAdmin".
///
/// Every Person defaults to <see cref="Employee"/>, including individual
/// (no-tenant) accounts, where this field is simply unused.
/// </summary>
public enum PersonOrgRole
{
    /// <summary>Regular employee — can see their own org context/role
    /// requirements, cannot manage the org structure.</summary>
    Employee = 0,

    /// <summary>A people-manager — future phases may grant them
    /// visibility into their direct reports' capability progress.
    /// Cannot manage the org structure (Departments/Roles) today.</summary>
    Manager = 1,

    /// <summary>HR / Learning &amp; Development admin for the tenant — the
    /// only role allowed to create/edit Departments and Job Roles, and
    /// (later phases) manage Role↔Capability training requirements and
    /// create private capabilities via Capability Studio.</summary>
    HRAdmin = 2,
}
