namespace HumanOS.Contracts.OrgStructure;

/// <summary>Response for GET /tenants/{tenantId}/job-roles and the
/// create/update job-role endpoints.</summary>
public sealed class JobRoleResponse
{
    public Guid JobRoleId { get; set; }

    public Guid TenantId { get; set; }

    public Guid? DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    public string Title { get; set; } = null!;

    public string? LevelLabel { get; set; }

    public string? Purpose { get; set; }

    public string? Summary { get; set; }

    public IReadOnlyList<string> Responsibilities { get; set; } = [];

    public string? RequiredExperience { get; set; }

    public string? RequiredEducation { get; set; }

    public IReadOnlyList<string> RequiredCertifications { get; set; } = [];

    public IReadOnlyList<string> RequiredLanguages { get; set; } = [];

    public IReadOnlyList<string> TechnicalSkills { get; set; } = [];

    public IReadOnlyList<string> SoftSkills { get; set; } = [];

    public IReadOnlyList<string> DepartmentProcesses { get; set; } = [];

    public IReadOnlyList<string> HrProcesses { get; set; } = [];

    public IReadOnlyList<string> Tools { get; set; } = [];

    public IReadOnlyList<string> StartInYourRoleTechnicalSkills { get; set; } = [];

    public IReadOnlyList<string> StartInYourRoleSoftSkills { get; set; } = [];

    public IReadOnlyList<string> StartInYourRoleDepartmentProcesses { get; set; } = [];

    public IReadOnlyList<string> StartInYourRoleHrProcesses { get; set; } = [];

    public IReadOnlyList<string> InYourRoleTechnicalSkills { get; set; } = [];

    public IReadOnlyList<string> InYourRoleSoftSkills { get; set; } = [];

    public IReadOnlyList<string> InYourRoleDepartmentProcesses { get; set; } = [];

    public IReadOnlyList<string> InYourRoleHrProcesses { get; set; } = [];

    public IReadOnlyList<string> DailyActivities { get; set; } = [];

    public IReadOnlyList<string> PerformanceIndicators { get; set; } = [];

    public IReadOnlyList<string> ExpectedOutcomes { get; set; } = [];

    public Guid? ReportsToJobRoleId { get; set; }

    public string? ReportsToJobRoleTitle { get; set; }

    /// <summary>Derived — true when at least one other JobRole in this
    /// tenant has <c>ReportsToJobRoleId == JobRoleId</c>. Not stored.</summary>
    public bool HasDirectReports { get; set; }

    public string? WorkSchedule { get; set; }

    public string? WorkModality { get; set; }

    public bool RequiresTravel { get; set; }

    public string? WorkLocation { get; set; }

    public string? AvailabilityNotes { get; set; }

    public string? EmploymentType { get; set; }

    public decimal? SalaryRangeMin { get; set; }

    public decimal? SalaryRangeMax { get; set; }

    public string? SalaryCurrency { get; set; }

    public IReadOnlyList<string> Benefits { get; set; } = [];

    public bool IsActive { get; set; }

    public int PersonCount { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }
}
