using System.Text.Json;
using HumanOS.Contracts.EmployeeScoring;
using HumanOS.Contracts.People;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Service responsible for gathering a single employee's complete professional profile
/// into an <see cref="EmployeeScoringProfile"/> DTO for downstream AI scoring against course engrams.
/// Strictly READ-ONLY.
/// </summary>
public sealed class EmployeeScoringProfileService
{
    private readonly HumanOsDbContext _dbContext;

    public EmployeeScoringProfileService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Builds and returns the complete employee scoring profile for the specified employee.
    /// </summary>
    public async Task<EmployeeScoringProfile> BuildProfileAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var profile = new EmployeeScoringProfile
        {
            EmployeeId = employeeId
        };

        // 1. Load Person with JobRole and Department
        var person = await _dbContext.People
            .AsNoTracking()
            .Include(p => p.JobRole)
            .Include(p => p.Department)
            .SingleOrDefaultAsync(p => p.PersonId == employeeId, cancellationToken);

        if (person is null)
        {
            return profile;
        }

        // 2. Load PersonProfile and current JobDescriptionRecord (if any)
        var personProfile = await _dbContext.PersonProfiles
            .AsNoTracking()
            .Include(pp => pp.CurrentJobDescription)
            .SingleOrDefaultAsync(pp => pp.PersonId == employeeId, cancellationToken);

        if (personProfile is not null)
        {
            profile.EmployeeName = !string.IsNullOrWhiteSpace(personProfile.DisplayName)
                ? personProfile.DisplayName.Trim()
                : $"{personProfile.FirstName} {personProfile.LastName}".Trim();
        }
        if (string.IsNullOrWhiteSpace(profile.EmployeeName))
        {
            profile.EmployeeName = person.Email ?? string.Empty;
        }

        // 3. Load PersonResume (if any)
        var personResume = await _dbContext.PersonResumes
            .AsNoTracking()
            .SingleOrDefaultAsync(pr => pr.PersonId == employeeId, cancellationToken);

        // 4. Load PersonCareerPlan (if any)
        var careerPlan = await _dbContext.PersonCareerPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(cp => cp.PersonId == employeeId, cancellationToken);

        // 5. Load Manager details if assigned
        string managerName = string.Empty;
        string managerTitle = string.Empty;
        if (person.ManagerId.HasValue && person.ManagerId.Value != Guid.Empty)
        {
            var manager = await _dbContext.People
                .AsNoTracking()
                .Include(p => p.JobRole)
                .SingleOrDefaultAsync(p => p.PersonId == person.ManagerId.Value, cancellationToken);

            var managerProfile = await _dbContext.PersonProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(pp => pp.PersonId == person.ManagerId.Value, cancellationToken);

            if (managerProfile is not null)
            {
                managerName = !string.IsNullOrWhiteSpace(managerProfile.DisplayName)
                    ? managerProfile.DisplayName.Trim()
                    : $"{managerProfile.FirstName} {managerProfile.LastName}".Trim();
            }

            if (string.IsNullOrWhiteSpace(managerName) && manager is not null)
            {
                managerName = manager.Email ?? string.Empty;
            }

            managerTitle = manager?.JobRole?.Title ?? managerProfile?.Occupation ?? string.Empty;
        }

        // 6. Resolve JobRole catalog items if JobRoleId is assigned
        List<string> catalogResponsibilities = [];
        List<string> catalogActivities = [];
        List<string> catalogJobSkills = [];
        List<string> catalogSoftSkills = [];
        List<string> catalogTools = [];

        if (person.JobRoleId.HasValue && person.JobRoleId.Value != Guid.Empty)
        {
            var jobRoleId = person.JobRoleId.Value;

            catalogResponsibilities = await _dbContext.JobRoleResponsibilities
                .AsNoTracking()
                .Where(jrr => jrr.JobRoleId == jobRoleId)
                .Select(jrr => jrr.Responsibility.Name)
                .ToListAsync(cancellationToken);

            catalogActivities = await _dbContext.JobRoleActivities
                .AsNoTracking()
                .Where(jra => jra.JobRoleId == jobRoleId)
                .Select(jra => jra.Activity.Name)
                .ToListAsync(cancellationToken);

            var skills = await _dbContext.JobRoleSkills
                .AsNoTracking()
                .Where(jrs => jrs.JobRoleId == jobRoleId)
                .Select(jrs => new { jrs.Skill.Name, jrs.Skill.Category })
                .ToListAsync(cancellationToken);

            catalogJobSkills = skills
                .Where(s => string.Equals(s.Category, "job-skills", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(s.Category, "technical", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Name)
                .ToList();

            catalogSoftSkills = skills
                .Where(s => string.Equals(s.Category, "soft-skills", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Name)
                .ToList();

            catalogTools = await _dbContext.JobRoleTools
                .AsNoTracking()
                .Where(jrt => jrt.JobRoleId == jobRoleId)
                .Select(jrt => jrt.Tool.Name)
                .ToListAsync(cancellationToken);
        }

        // --------------------------------------------------------------------
        // SECTION 1: ROLE / JOB DESCRIPTION
        // --------------------------------------------------------------------
        var jobRole = person.JobRole;
        var currentJd = personProfile?.CurrentJobDescription;

        profile.RoleTitle = jobRole?.Title
            ?? currentJd?.JobTitle
            ?? personProfile?.Occupation
            ?? string.Empty;

        profile.RolePurpose = jobRole?.Purpose
            ?? currentJd?.RolePurpose
            ?? jobRole?.Summary
            ?? string.Empty;

        var roleResponsibilities = DeserializeStringList(jobRole?.ResponsibilitiesJson);
        var jdResponsibilities = DeserializeStringList(currentJd?.PrimaryResponsibilitiesJson);
        profile.CoreResponsibilities = UnionLists(roleResponsibilities, catalogResponsibilities, jdResponsibilities);

        var roleActivities = DeserializeStringList(jobRole?.DailyActivitiesJson);
        profile.DailyActivities = UnionLists(roleActivities, catalogActivities);

        // --------------------------------------------------------------------
        // SECTION 2: OBJECTIVES (SMART objectives / goals) -> ActiveObjectives
        // --------------------------------------------------------------------
        var personGoals = await _dbContext.PersonGoals
            .AsNoTracking()
            .Include(pg => pg.Goal)
            .Where(pg => pg.PersonId == employeeId && pg.Status == "Active")
            .OrderByDescending(pg => pg.StartedDate)
            .ToListAsync(cancellationToken);

        if (personGoals.Count > 0)
        {
            profile.ActiveObjectives = personGoals.Select(pg =>
            {
                var date = pg.TargetDate ?? pg.StartedDate;
                var quarter = $"Q{(date.Month - 1) / 3 + 1} {date.Year}";

                return new ObjectiveInfo
                {
                    Title = pg.Goal?.Name ?? string.Empty,
                    Metric = pg.ProgressPercentage > 0 ? $"{pg.ProgressPercentage}%" : string.Empty,
                    Quarter = quarter,
                    Status = pg.Status ?? "Active",
                    AlignedDeptObjective = string.Empty,
                    CorporateGoal = pg.Goal?.Category ?? string.Empty,
                    ValueImpact = pg.Goal?.Description ?? string.Empty,
                    ActionPlan = string.Empty,
                    MeasurementMethod = string.Empty
                };
            }).ToList();
        }
        else
        {
            // Fallback to active departmental / role SMART objectives — kept
            // in sync with human-os-employee's DEFAULT_EMPLOYEE_OBJECTIVES_*
            // mock data (frontend/human-os-employee/src/features/objectives/
            // api/employeeObjectivesApi.ts), since this employee has no real
            // PersonGoal rows yet. 2026-09-04 fix: this used to be a plain
            // "Optimize P95 Latency..." microservices objective with NO
            // mention of AI Agents at all — every voice/text agent that
            // reads ActiveObjectives (e.g. the Structured Learning Teaching
            // step's "why this concept matters" personalization) then had no
            // real objective to correctly connect an AI-Agents/prompt-
            // engineering Engram to, and either stayed silent or wrongly
            // attached it to the unrelated microservices objective instead.
            // 2026-09-04 (2nd fix): title/description replaced again with
            // the employee's ACTUAL real objective (as typed by the user),
            // since "Build the Autonomous Code Verification..." still wasn't
            // specific enough and kept getting summarized by the model as a
            // plain "API" objective. The agent's own build step (microservice
            // + API + orchestration) is only the DELIVERY MECHANISM — the
            // objective is explicit that the actual capability gap is
            // designing the prompts that drive the agent's reasoning, so a
            // Prompt Engineering Engram must connect here, never to "APIs".
            profile.ActiveObjectives =
            [
                new ObjectiveInfo
                {
                    Title = "Design, Build, and Deploy One Production Autonomous AI Agent to Automate Functional Divisions' Service-Request Triage",
                    Metric = "Percentage of Functional Divisions service requests triaged autonomously",
                    Quarter = "Q4 2026",
                    Status = "Active",
                    AlignedDeptObjective = "Deploy AI Agents Across Key Operational Workflows",
                    CorporateGoal = "Deploy AI Agents Across Key Operational Workflows",
                    ValueImpact = "Deliver a production-grade autonomous AI agent (microservice + API + orchestration) that automatically triages incoming Functional Divisions service requests. The microservice/API/orchestration layer is only the delivery mechanism — the actual capability gap this objective depends on is PROMPT ENGINEERING: designing the structured prompts and reasoning strategy that let the agent correctly interpret, classify, and route each request.",
                    ActionPlan = "1. Design structured prompts using Microsoft Agents SDK. 2. Implement the triage/classification logic driven by those prompts. 3. Wire the agent into the Functional Divisions service-request microservice/API/orchestration layer.",
                    MeasurementMethod = "Automated triage accuracy telemetry against a labeled backlog of past service requests"
                },
                new ObjectiveInfo
                {
                    Title = "Complete Cloud Security & OWASP Top 10 Hardening",
                    Metric = "Zero critical or high security audit findings on public endpoints",
                    Quarter = "Q4 2026",
                    Status = "Active",
                    AlignedDeptObjective = "Enterprise Security & Zero Trust Architecture",
                    CorporateGoal = "Security, Privacy and Regulatory Compliance",
                    ValueImpact = "Protects customer data integrity and prevents platform vulnerability exposures",
                    ActionPlan = "Implement strict OAuth2/OIDC token validation, rate limiting, and input sanitization.",
                    MeasurementMethod = "Automated SAST/DAST security scans and penetration test sign-offs"
                }
            ];
        }

        // --------------------------------------------------------------------
        // SECTION 3: KPIs / PERFORMANCE INDICATORS -> Kpis
        // --------------------------------------------------------------------
        var roleKpis = DeserializeStringList(jobRole?.PerformanceIndicatorsJson);
        profile.Kpis = roleKpis;

        // --------------------------------------------------------------------
        // SECTION 4: COACHING / GROWTH AREAS -> CoachingNotes
        // --------------------------------------------------------------------
        var coachingList = new List<CoachingInfo>();
        if (careerPlan is not null)
        {
            var planDate = careerPlan.AcceptedDate ?? careerPlan.GeneratedDate;
            var planQuarter = planDate != default
                ? $"Q{(planDate.Month - 1) / 3 + 1} {planDate.Year}"
                : "Q1 2026";

            var technicalSteps = DeserializeRoadmapSteps(careerPlan.TechnicalRoadmapJson);
            foreach (var step in technicalSteps)
            {
                var isObservability = step.Skill.Contains("testing", StringComparison.OrdinalIgnoreCase) ||
                                     step.Skill.Contains("debugging", StringComparison.OrdinalIgnoreCase) ||
                                     step.Skill.Contains("signal", StringComparison.OrdinalIgnoreCase);

                coachingList.Add(new CoachingInfo
                {
                    CompetencyType = "Technical",
                    CompetencyName = !string.IsNullOrWhiteSpace(step.Skill) ? step.Skill : step.Title,
                    Quarter = planQuarter,
                    Status = step.TrainingStatus == "Completed" ? "Applied in Role" : "In Progress",
                    CoachingNote = !string.IsNullOrWhiteSpace(step.Description)
                        ? step.Description
                        : (careerPlan.OverallRationale ?? string.Empty),
                    ManagerName = string.IsNullOrWhiteSpace(managerName) ? "Carlos Morales" : managerName,
                    ManagerTitle = string.IsNullOrWhiteSpace(managerTitle) ? "Director of Software Engineering" : managerTitle,
                    Date = planDate != default ? planDate : DateTime.UtcNow.AddDays(-20),
                    ImpactsSmartObjective = isObservability ? "Design, Build, and Deploy One Production Autonomous AI Agent to Automate Functional Divisions' Service-Request Triage" : "Complete Cloud Security & OWASP Top 10 Hardening",
                    RecommendedLearningTrack = step.MatchedProgramName
                        ?? step.MatchedCapabilityName
                        ?? step.Title,
                    GrowthMasteryProgress = step.TrainingStatus == "Completed" ? 100 : (step.TrainingStatus == "InProgress" ? 50 : 35),
                    Milestones =
                    [
                        new() { Description = "Complete required architecture modules and assessment", IsDone = step.TrainingStatus == "Completed" },
                        new() { Description = "Apply design patterns to production services", IsDone = false }
                    ]
                });
            }

            var softSteps = DeserializeRoadmapSteps(careerPlan.SoftSkillsRoadmapJson);
            foreach (var step in softSteps)
            {
                coachingList.Add(new CoachingInfo
                {
                    CompetencyType = "Soft Skill & Leadership",
                    CompetencyName = !string.IsNullOrWhiteSpace(step.Skill) ? step.Skill : step.Title,
                    Quarter = planQuarter,
                    Status = step.TrainingStatus == "Completed" ? "Applied in Role" : "In Progress",
                    CoachingNote = !string.IsNullOrWhiteSpace(step.Description)
                        ? step.Description
                        : (careerPlan.OverallRationale ?? string.Empty),
                    ManagerName = string.IsNullOrWhiteSpace(managerName) ? "Carlos Morales" : managerName,
                    ManagerTitle = string.IsNullOrWhiteSpace(managerTitle) ? "Director of Software Engineering" : managerTitle,
                    Date = planDate != default ? planDate : DateTime.UtcNow.AddDays(-15),
                    ImpactsSmartObjective = "Design, Build, and Deploy One Production Autonomous AI Agent to Automate Functional Divisions' Service-Request Triage",
                    RecommendedLearningTrack = step.MatchedProgramName
                        ?? step.MatchedCapabilityName
                        ?? step.Title,
                    GrowthMasteryProgress = step.TrainingStatus == "Completed" ? 100 : (step.TrainingStatus == "InProgress" ? 50 : 40),
                    Milestones =
                    [
                        new() { Description = "Structure technical proposals in standard ADR format", IsDone = true },
                        new() { Description = "Lead technical review session with cross-functional stakeholders", IsDone = false }
                    ]
                });
            }
        }

        if (coachingList.Count == 0)
        {
            coachingList.Add(new CoachingInfo
            {
                CompetencyType = "Technical",
                CompetencyName = "API Observability & Distributed Tracing",
                Quarter = "Q1 2026",
                Status = "In Progress",
                CoachingNote = "To support our quarterly P95 latency and SLA objectives, deepen expertise in OpenTelemetry instrumentation and resilience patterns (circuit breakers, retry policies).",
                ManagerName = string.IsNullOrWhiteSpace(managerName) ? "Carlos Morales" : managerName,
                ManagerTitle = string.IsNullOrWhiteSpace(managerTitle) ? "Director of Software Engineering" : managerTitle,
                Date = DateTime.UtcNow.AddDays(-20),
                ImpactsSmartObjective = "Design, Build, and Deploy One Production Autonomous AI Agent to Automate Functional Divisions' Service-Request Triage",
                RecommendedLearningTrack = "AI AGENTS + PROMPT ENGINEERING",
                GrowthMasteryProgress = 65,
                Milestones =
                [
                    new() { Description = "Complete Chapters 1-4 of the Structured Learning course in Engram", IsDone = true },
                    new() { Description = "Integrate OpenTelemetry SDK with exporter in Auth Service", IsDone = true },
                    new() { Description = "Build P95 latency and error rate dashboard in Grafana", IsDone = false }
                ]
            });

            coachingList.Add(new CoachingInfo
            {
                CompetencyType = "Soft Skill & Leadership",
                CompetencyName = "Cross-Functional Communication & Architecture Decision Records (ADRs)",
                Quarter = "Q1 2026",
                Status = "In Progress",
                CoachingNote = "Structure technical proposals in executive ADRs to facilitate consensus with Product and Security without friction.",
                ManagerName = string.IsNullOrWhiteSpace(managerName) ? "Valeria Sotomayor" : managerName,
                ManagerTitle = string.IsNullOrWhiteSpace(managerTitle) ? "VP of Technology & Product" : managerTitle,
                Date = DateTime.UtcNow.AddDays(-15),
                ImpactsSmartObjective = "Complete Cloud Security & OWASP Top 10 Hardening",
                RecommendedLearningTrack = "Technical Communication & Architecture Governance",
                GrowthMasteryProgress = 80,
                Milestones =
                [
                    new() { Description = "Adopt standard ADR template for autonomous agents", IsDone = true },
                    new() { Description = "Facilitate architecture review session with Cybersecurity team", IsDone = true }
                ]
            });
        }

        profile.CoachingNotes = coachingList;

        // --------------------------------------------------------------------
        // SECTION 5: SKILLS (TechnicalSkills, SoftSkills, ResumeSkills)
        // --------------------------------------------------------------------
        var roleTechSkills = DeserializeStringList(jobRole?.TechnicalSkillsJson);
        var roleDeptProcesses = DeserializeStringList(jobRole?.DepartmentProcessesJson);
        profile.TechnicalSkills = UnionLists(roleTechSkills, catalogJobSkills, roleDeptProcesses);

        var roleSoftSkills = DeserializeStringList(jobRole?.SoftSkillsJson);
        var roleHrProcesses = DeserializeStringList(jobRole?.HrProcessesJson);
        profile.SoftSkills = UnionLists(roleSoftSkills, catalogSoftSkills, roleHrProcesses);

        var resumeSkills = DeserializeStringList(personResume?.SkillsJson);
        profile.ResumeSkills = resumeSkills;

        // --------------------------------------------------------------------
        // SECTION 6: TOOLS & SOFTWARE -> Tools
        // --------------------------------------------------------------------
        var roleTools = DeserializeStringList(jobRole?.ToolsJson);
        var jdTools = DeserializeStringList(currentJd?.ToolsMentionedJson);
        profile.Tools = UnionLists(roleTools, catalogTools, jdTools);

        // --------------------------------------------------------------------
        // SECTION 7: EXPECTED SUCCESS OUTCOMES -> ExpectedSuccessOutcomes
        // --------------------------------------------------------------------
        var roleOutcomes = DeserializeStringList(jobRole?.ExpectedOutcomesJson);
        var jdOutcomes = DeserializeStringList(currentJd?.ExpectedOutcomesJson);
        profile.ExpectedSuccessOutcomes = UnionLists(roleOutcomes, jdOutcomes);

        // --------------------------------------------------------------------
        // SECTION 8: RÉSUMÉ / CV (Summary, Experience, Education, Certifications)
        // --------------------------------------------------------------------
        profile.ResumeSummary = !string.IsNullOrWhiteSpace(personResume?.ProfessionalSummary)
            ? personResume.ProfessionalSummary.Trim()
            : (!string.IsNullOrWhiteSpace(personResume?.ProfessionalObjective)
                ? personResume.ProfessionalObjective.Trim()
                : (personProfile?.Biography ?? string.Empty));

        var rawWorkExp = DeserializeStringList(personResume?.WorkExperienceJson);
        profile.WorkExperience = rawWorkExp.Select(ParseWorkExperience).ToList();

        var rawEducation = DeserializeStringList(personResume?.EducationJson);
        profile.Education = rawEducation.Select(ParseEducation).ToList();

        profile.Certifications = DeserializeStringList(personResume?.CertificationsJson);

        profile.YearsOfExperience = CalculateYearsOfExperience(profile.WorkExperience);

        return profile;
    }

    private static WorkExperienceInfo ParseWorkExperience(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return new WorkExperienceInfo();
        }

        var trimmed = entry.Trim();

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<WorkExperienceInfo>(trimmed);
                if (parsed is not null) return parsed;
            }
            catch { }
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            trimmed,
            @"^(?<company>[^—\-:]+?)\s*[—\-]\s*(?<title>[^:(]+?)(?:\s*\((?<dates>[^)]+)\))?(?:\s*:\s*(?<desc>.*))?$",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (match.Success)
        {
            var company = match.Groups["company"].Value.Trim();
            var title = match.Groups["title"].Value.Trim();
            var dates = match.Groups["dates"].Value.Trim();
            var desc = match.Groups["desc"].Value.Trim();

            DateTime? startDate = null;
            DateTime? endDate = null;

            if (!string.IsNullOrWhiteSpace(dates))
            {
                var dateParts = System.Text.RegularExpressions.Regex.Split(dates, @"\s*[-–/to]+\s*", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (dateParts.Length >= 1 && int.TryParse(System.Text.RegularExpressions.Regex.Match(dateParts[0], @"\d{4}").Value, out var startYear))
                {
                    startDate = new DateTime(startYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                }

                if (dateParts.Length >= 2)
                {
                    var endStr = dateParts[1].Trim().ToLowerInvariant();
                    if (endStr is "present" or "actual" or "current" or "hoy" or "presente")
                    {
                        endDate = null;
                    }
                    else if (int.TryParse(System.Text.RegularExpressions.Regex.Match(endStr, @"\d{4}").Value, out var endYear))
                    {
                        endDate = new DateTime(endYear, 12, 31, 0, 0, 0, DateTimeKind.Utc);
                    }
                }
            }

            return new WorkExperienceInfo
            {
                Company = company,
                JobTitle = title,
                StartDate = startDate,
                EndDate = endDate,
                Description = !string.IsNullOrWhiteSpace(desc) ? desc : trimmed
            };
        }

        return new WorkExperienceInfo
        {
            Description = trimmed
        };
    }

    private static EducationInfo ParseEducation(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return new EducationInfo();
        }

        var trimmed = entry.Trim();

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<EducationInfo>(trimmed);
                if (parsed is not null) return parsed;
            }
            catch { }
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            trimmed,
            @"^(?<inst>[^—\-]+?)\s*[—\-]\s*(?<degree>[^(:]+?)(?:\s*\((?<year>[^)]+)\))?(?:\s*:\s*(?<field>.*))?$",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        if (match.Success)
        {
            var inst = match.Groups["inst"].Value.Trim();
            var degree = match.Groups["degree"].Value.Trim();
            var year = match.Groups["year"].Value.Trim();
            var field = match.Groups["field"].Value.Trim();

            return new EducationInfo
            {
                Institution = inst,
                Degree = degree,
                Field = !string.IsNullOrWhiteSpace(field) ? field : degree,
                Year = year
            };
        }

        return new EducationInfo
        {
            Degree = trimmed
        };
    }

    private static int? CalculateYearsOfExperience(List<WorkExperienceInfo> workExperience)
    {
        if (workExperience == null || workExperience.Count == 0)
        {
            return null; // TODO: confirm resume source table
        }

        double totalDays = 0;
        bool hasAnyDates = false;

        foreach (var exp in workExperience)
        {
            if (exp.StartDate.HasValue)
            {
                hasAnyDates = true;
                var end = exp.EndDate ?? DateTime.UtcNow;
                if (end > exp.StartDate.Value)
                {
                    totalDays += (end - exp.StartDate.Value).TotalDays;
                }
            }
        }

        if (hasAnyDates && totalDays > 0)
        {
            return (int)Math.Round(totalDays / 365.25);
        }

        return null; // TODO: confirm resume source table
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<CareerPlanRoadmapStepDto> DeserializeRoadmapSteps(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<CareerPlanRoadmapStepDto>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<string> UnionLists(params IEnumerable<string>?[] lists)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var list in lists)
        {
            if (list is null) continue;

            foreach (var item in list)
            {
                if (!string.IsNullOrWhiteSpace(item) && seen.Add(item.Trim()))
                {
                    result.Add(item.Trim());
                }
            }
        }

        return result;
    }
}
