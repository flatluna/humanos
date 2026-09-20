namespace HumanOS.Contracts.EmployeeScoring;

/// <summary>
/// Consolidated professional profile for an employee used for AI scoring against course engrams.
/// </summary>
public sealed class EmployeeScoringProfile
{
    public Guid EmployeeId { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    public string RoleTitle { get; set; } = string.Empty;

    public string RolePurpose { get; set; } = string.Empty;

    public List<string> CoreResponsibilities { get; set; } = [];

    public List<string> DailyActivities { get; set; } = [];

    public List<ObjectiveInfo> ActiveObjectives { get; set; } = [];

    public List<string> Kpis { get; set; } = [];

    public List<CoachingInfo> CoachingNotes { get; set; } = [];

    public List<string> TechnicalSkills { get; set; } = [];

    public List<string> SoftSkills { get; set; } = [];

    public List<string> Tools { get; set; } = [];

    public List<string> ExpectedSuccessOutcomes { get; set; } = [];

    /// <summary>
    /// Professional summary / bio from the employee's CV or profile.
    /// </summary>
    public string ResumeSummary { get; set; } = string.Empty;

    /// <summary>
    /// Total years of professional experience, if available.
    /// </summary>
    public int? YearsOfExperience { get; set; }

    /// <summary>
    /// Work experience entries from the employee's CV.
    /// </summary>
    public List<WorkExperienceInfo> WorkExperience { get; set; } = [];

    /// <summary>
    /// Education entries from the employee's CV.
    /// </summary>
    public List<EducationInfo> Education { get; set; } = [];

    /// <summary>
    /// Certifications held by the employee (from CV).
    /// </summary>
    public List<string> Certifications { get; set; } = [];

    /// <summary>
    /// Skills claimed / stated on the CV by the employee (kept separate from company-assigned skills).
    /// </summary>
    public List<string> ResumeSkills { get; set; } = [];

    /// <summary>
    /// Set only when this person has a real Tec de Monterrey student academic profile on file
    /// (institution/degree/grades) — signals this is a STUDENT, not a HumanOS employee, so callers
    /// (e.g. LearnerContextBuilder) can skip job-role/objectives framing entirely for them.
    /// </summary>
    public string? StudentAcademicProfileSummary { get; set; }
}

/// <summary>
/// SMART objective / goal information for an employee.
/// </summary>
public sealed class ObjectiveInfo
{
    public string Title { get; set; } = string.Empty;

    public string Metric { get; set; } = string.Empty;

    public string Quarter { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string AlignedDeptObjective { get; set; } = string.Empty;

    public string CorporateGoal { get; set; } = string.Empty;

    public string ValueImpact { get; set; } = string.Empty;

    public string ActionPlan { get; set; } = string.Empty;

    public string MeasurementMethod { get; set; } = string.Empty;
}

/// <summary>
/// Coaching and competency growth area for an employee.
/// </summary>
public sealed class CoachingInfo
{
    public string CompetencyType { get; set; } = string.Empty;            // "Technical" or "Soft Skill & Leadership"

    public string CompetencyName { get; set; } = string.Empty;            // e.g. "Time management"

    public string Quarter { get; set; } = string.Empty;                   // e.g. "Q1 2026"

    public string Status { get; set; } = string.Empty;                    // "In Progress" | "Applied in Role"

    public string CoachingNote { get; set; } = string.Empty;              // the manager's quote

    public string ManagerName { get; set; } = string.Empty;

    public string ManagerTitle { get; set; } = string.Empty;

    public DateTime? Date { get; set; }

    public string ImpactsSmartObjective { get; set; } = string.Empty;     // title of the linked SMART objective

    public string RecommendedLearningTrack { get; set; } = string.Empty;  // manager-suggested learning track

    public int GrowthMasteryProgress { get; set; }                        // 0-100 percent

    public List<CoachingMilestone> Milestones { get; set; } = [];
}

/// <summary>
/// Specific milestone within a coaching growth area.
/// </summary>
public sealed class CoachingMilestone
{
    public string Description { get; set; } = string.Empty;

    public bool IsDone { get; set; }
}

/// <summary>
/// Work experience entry from the employee's CV.
/// </summary>
public sealed class WorkExperienceInfo
{
    public string JobTitle { get; set; } = string.Empty;

    public string Company { get; set; } = string.Empty;

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; } // null = current

    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Education entry from the employee's CV.
/// </summary>
public sealed class EducationInfo
{
    public string Degree { get; set; } = string.Empty;

    public string Institution { get; set; } = string.Empty;

    public string Field { get; set; } = string.Empty;

    public string Year { get; set; } = string.Empty;
}
