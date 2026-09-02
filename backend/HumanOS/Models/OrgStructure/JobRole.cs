namespace HumanOS.Models.OrgStructure;

using HumanOS.Models.Tenancy;

/// <summary>
/// An organization-authored "puesto" (position) template that lives inside
/// a <see cref="Department"/> and is reused across every
/// <see cref="People.Person"/> assigned to it (see
/// <c>Person.JobRoleId</c>).
///
/// Deliberately distinct from
/// <see cref="HumanOS.Models.JobDescriptions.JobDescriptionRecord"/>: that
/// record is a per-PERSON document extracted from an uploaded PDF via
/// <see cref="HumanOS.Agents.JobDescriptionExtractionAgent"/> and requires
/// employee confirmation. A JobRole is authored directly by an HRAdmin for
/// the whole organization — no PDF/agent/confirmation workflow involved.
///
/// Role↔Capability training requirements/certifications and the career
/// roadmap (Role → next Role) are later phases — not modeled on this
/// entity yet.
/// </summary>
public sealed class JobRole
{
    public Guid JobRoleId { get; set; }

    public Guid TenantId { get; set; }

    public Guid? DepartmentId { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>Free-text seniority label (e.g. "Junior", "Senior",
    /// "Lead") — deliberately not an enum, since organizations use very
    /// different career ladders.</summary>
    public string? LevelLabel { get; set; }

    public string? Purpose { get; set; }

    public string? Summary { get; set; }

    /// <summary>JSON-serialized string[] — mirrors
    /// JobDescriptionRecord.PrimaryResponsibilitiesJson's convention
    /// (always read/written as a whole list, no separate child table).</summary>
    public string ResponsibilitiesJson { get; set; } = "[]";

    public string? RequiredExperience { get; set; }

    /*
     * Full job-description fields (added so "Confirmar y crear" / the
     * Roles y Puestos editor can capture a complete "descripción de
     * trabajo", not just title + responsibilities). All list-shaped
     * fields follow the same JSON string[] convention as
     * ResponsibilitiesJson above.
     */

    public string? RequiredEducation { get; set; }

    /// <summary>JSON string[] — certifications required for the role.</summary>
    public string RequiredCertificationsJson { get; set; } = "[]";

    /// <summary>JSON string[] — required languages (e.g. "Inglés avanzado").</summary>
    public string RequiredLanguagesJson { get; set; } = "[]";

    /// <summary>JSON string[] — technical knowledge / hard skills.</summary>
    public string TechnicalSkillsJson { get; set; } = "[]";

    /// <summary>JSON string[] — soft skills / competencies.</summary>
    public string SoftSkillsJson { get; set; } = "[]";

    /// <summary>JSON string[] — required department-specific processes an
    /// employee in this role must complete (e.g. "Proceso de armado de
    /// reportes de ventas") — feeds the "Procesos de Depto." career-plan
    /// track, same rationale/shape as TechnicalSkillsJson/SoftSkillsJson.</summary>
    public string DepartmentProcessesJson { get; set; } = "[]";

    /// <summary>JSON string[] — required HR/compliance processes and
    /// policies (e.g. "Prevención de Acoso Sexual Laboral") — feeds the
    /// "Procesos de RRHH" career-plan track.</summary>
    public string HrProcessesJson { get; set; } = "[]";

    /// <summary>JSON string[] — tools, software or technologies the role
    /// must use (e.g. "Excel", "Power BI", "GitHub").</summary>
    public string ToolsJson { get; set; } = "[]";

    /// <summary>JSON string[] — day-to-day or periodic activities.</summary>
    public string DailyActivitiesJson { get; set; } = "[]";

    /// <summary>JSON string[] — performance indicators / KPIs.</summary>
    public string PerformanceIndicatorsJson { get; set; } = "[]";

    /// <summary>JSON string[] — expected outcomes/goals for the role
    /// ("objetivos del puesto"), distinct from <see cref="Purpose"/> (why
    /// the role exists) — this is what success looks like.</summary>
    public string ExpectedOutcomesJson { get; set; } = "[]";

    /// <summary>Another JobRole in the same tenant that this role reports
    /// to (e.g. "Ejecutivo de Ventas" → "Gerente de Ventas"). Null for
    /// top-level roles. Whether the role itself has people reporting to
    /// it is derived (see JobRoleResponse.HasDirectReports), not stored.</summary>
    public Guid? ReportsToJobRoleId { get; set; }

    public string? WorkSchedule { get; set; }

    /// <summary>Free text, e.g. "Presencial", "Híbrido", "Remoto" —
    /// deliberately not an enum, same rationale as <see cref="LevelLabel"/>.</summary>
    public string? WorkModality { get; set; }

    public bool RequiresTravel { get; set; }

    public string? WorkLocation { get; set; }

    public string? AvailabilityNotes { get; set; }

    /// <summary>Free text, e.g. "Tiempo completo", "Medio tiempo",
    /// "Freelance", "Por proyecto", "Honorarios".</summary>
    public string? EmploymentType { get; set; }

    public decimal? SalaryRangeMin { get; set; }

    public decimal? SalaryRangeMax { get; set; }

    public string? SalaryCurrency { get; set; }

    /// <summary>JSON string[] — bonos, comisiones, prestaciones, equipo de
    /// trabajo, etc.</summary>
    public string BenefitsJson { get; set; } = "[]";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Tenant Tenant { get; set; } = null!;

    public Department? Department { get; set; }

    public JobRole? ReportsToJobRole { get; set; }

    public ICollection<JobRole> DirectReportRoles { get; set; } = [];

    public ICollection<DepartmentJobRole> DepartmentJobRoles { get; set; } = [];
}
