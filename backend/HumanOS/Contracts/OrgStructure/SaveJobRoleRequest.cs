namespace HumanOS.Contracts.OrgStructure;

/// <summary>Body for POST /tenants/{tenantId}/job-roles and
/// PUT /job-roles/{jobRoleId}.</summary>
public sealed class SaveJobRoleRequest
{
    public Guid? DepartmentId { get; set; }

    public string Title { get; set; } = null!;

    public string? LevelLabel { get; set; }

    public string? Purpose { get; set; }

    public string? Summary { get; set; }

    public List<string>? Responsibilities { get; set; }

    public string? RequiredExperience { get; set; }

    public string? RequiredEducation { get; set; }

    public List<string>? RequiredCertifications { get; set; }

    public List<string>? RequiredLanguages { get; set; }

    public List<string>? TechnicalSkills { get; set; }

    public List<string>? SoftSkills { get; set; }

    public List<string>? DepartmentProcesses { get; set; }

    public List<string>? HrProcesses { get; set; }

    public List<string>? Tools { get; set; }

    public List<string>? DailyActivities { get; set; }

    public List<string>? PerformanceIndicators { get; set; }

    public List<string>? ExpectedOutcomes { get; set; }

    public Guid? ReportsToJobRoleId { get; set; }

    public string? WorkSchedule { get; set; }

    public string? WorkModality { get; set; }

    public bool RequiresTravel { get; set; }

    public string? WorkLocation { get; set; }

    public string? AvailabilityNotes { get; set; }

    public string? EmploymentType { get; set; }

    public decimal? SalaryRangeMin { get; set; }

    public decimal? SalaryRangeMax { get; set; }

    public string? SalaryCurrency { get; set; }

    public List<string>? Benefits { get; set; }
}
