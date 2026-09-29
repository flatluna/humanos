namespace HumanOS.Contracts.OrgStructure;

/// <summary>Body for POST /tenants/{tenantId}/job-roles/generate-description — the
/// CURRENT draft values of the Roles y Puestos editor form (same field shape as
/// <see cref="SaveJobRoleRequest"/>) plus which existing JobRoleId (if any) is being
/// edited, so the agent's search_similar_job_roles tool never benchmarks a role against
/// its own current draft.</summary>
public sealed class GenerateJobRoleDescriptionRequest
{
    public string? LanguageCode { get; set; }

    public Guid? JobRoleId { get; set; }

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

    public List<string>? Tools { get; set; }

    public List<string>? DailyActivities { get; set; }

    public List<string>? PerformanceIndicators { get; set; }

    public List<string>? ExpectedOutcomes { get; set; }

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

/// <summary>Response for the same endpoint — the agent's field-by-field proposal, plus
/// token usage for observability. The frontend diffs this against the CURRENT form
/// values itself (client-side) to render the before/after review UI — this response
/// does not decide what changed, and nothing in it is ever saved automatically.</summary>
public sealed class GenerateJobRoleDescriptionResponse
{
    public string? LevelLabel { get; set; }

    public string? Purpose { get; set; }

    public string? Summary { get; set; }

    public List<string> Responsibilities { get; set; } = [];

    public string? RequiredExperience { get; set; }

    public string? RequiredEducation { get; set; }

    public List<string> RequiredCertifications { get; set; } = [];

    public List<string> RequiredLanguages { get; set; } = [];

    public List<string> TechnicalSkills { get; set; } = [];

    public List<string> SoftSkills { get; set; } = [];

    public List<string> Tools { get; set; } = [];

    public List<string> DailyActivities { get; set; } = [];

    public List<string> PerformanceIndicators { get; set; } = [];

    public List<string> ExpectedOutcomes { get; set; } = [];

    public string? WorkSchedule { get; set; }

    public string? WorkModality { get; set; }

    public bool RequiresTravel { get; set; }

    public string? WorkLocation { get; set; }

    public string? AvailabilityNotes { get; set; }

    public string? EmploymentType { get; set; }

    public decimal? SalaryRangeMin { get; set; }

    public decimal? SalaryRangeMax { get; set; }

    public string? SalaryCurrency { get; set; }

    public List<string> Benefits { get; set; } = [];

    /// <summary>The Azure OpenAI deployment that produced this proposal (observability only).</summary>
    public string ModelName { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }
}
