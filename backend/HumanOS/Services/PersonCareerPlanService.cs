using System.Text.Json;
using HumanOS.Agents;
using HumanOS.Contracts.People;
using HumanOS.Data;
using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>Get/generate for a Person's AI career development plan — see
/// <see cref="PersonCareerPlan"/>'s own doc comment. One optional row per
/// Person, regenerated in place (no history) each time "Generar plan con
/// IA" is run.</summary>
public sealed class PersonCareerPlanService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly CareerDevelopmentPlanAgent _agent;
    private readonly GrowthPlanService _growthPlanService;
    private readonly CapabilityEnrollmentService _capabilityEnrollmentService;

    public PersonCareerPlanService(
        HumanOsDbContext dbContext,
        CareerDevelopmentPlanAgent agent,
        GrowthPlanService growthPlanService,
        CapabilityEnrollmentService capabilityEnrollmentService)
    {
        _dbContext = dbContext;
        _agent = agent;
        _growthPlanService = growthPlanService;
        _capabilityEnrollmentService = capabilityEnrollmentService;
    }

    /// <summary>Never returns null — a person with no plan yet gets
    /// HasPlan=false so the frontend can always render a "Generar" state.</summary>
    public async Task<PersonCareerPlanResponse> GetByPersonIdAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _dbContext.PersonCareerPlans
            .SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);

        if (plan is null)
        {
            return new PersonCareerPlanResponse { PersonId = personId, HasPlan = false };
        }

        await AutoMatchCapabilitiesAsync(plan, personId, cancellationToken);

        var jobRoleTitle = await _dbContext.JobRoles
            .AsNoTracking()
            .Where(r => r.JobRoleId == plan.JobRoleId)
            .Select(r => r.Title)
            .SingleOrDefaultAsync(cancellationToken);

        var response = ToResponse(plan, jobRoleTitle);
        await ApplyTrainingStatusAsync(response, personId, cancellationToken);
        return response;
    }

    /// <summary>Runs the real LLM analysis and persists the result as the
    /// person's latest plan (replacing any previous one).</summary>
    public async Task<PersonCareerPlanResponse> GenerateAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var person = await _dbContext.People
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken)
            ?? throw new KeyNotFoundException("Person not found.");

        if (person.JobRoleId is null)
        {
            throw new InvalidOperationException(
                "This employee has no job role assigned yet. Assign a Puesto first.");
        }

        var jobRole = await _dbContext.JobRoles
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.JobRoleId == person.JobRoleId, cancellationToken)
            ?? throw new KeyNotFoundException("The employee's assigned job role was not found.");

        var resume = await _dbContext.PersonResumes
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.PersonId == personId, cancellationToken);

        var currentSkills = resume is null
            ? []
            : JsonSerializer.Deserialize<List<string>>(resume.SkillsJson) ?? [];

        var profile = await _dbContext.PersonProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(pp => pp.PersonId == personId, cancellationToken);

        var personName = profile?.DisplayName
            ?? (profile is null ? null : $"{profile.FirstName} {profile.LastName}".Trim())
            ?? person.Email
            ?? "Employee";

        var realPrograms = await _dbContext.Programs
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new RealProgramOption
            {
                ProgramId = p.ProgramId,
                Name = p.Name,
                Description = p.Description ?? string.Empty,
            })
            .ToListAsync(cancellationToken);

        // UNION each requirement list with the shared Skill catalog
        // (2026-08-01 "cierra el ciclo" feature) — a Skill authored once
        // and bulk-linked to many JobRoles (e.g. "Prevención de Acoso
        // Sexual Laboral" required for every role) must feed the gap
        // analysis here exactly like a name typed directly into this
        // role's own free-text list. See SkillService.UnionWithCatalog.
        var catalogByCategory = await SkillService.GetCatalogNamesByCategoryAsync(_dbContext, jobRole.JobRoleId, cancellationToken);

        var proposal = await _agent.GenerateAsync(
            new CareerPlanRequestContext
            {
                PersonName = personName,
                JobRoleTitle = jobRole.Title,
                RequiredTechnicalSkills = SkillService.UnionWithCatalog(
                    JsonSerializer.Deserialize<List<string>>(jobRole.TechnicalSkillsJson) ?? [], catalogByCategory, "job-skills"),
                RequiredSoftSkills = SkillService.UnionWithCatalog(
                    JsonSerializer.Deserialize<List<string>>(jobRole.SoftSkillsJson) ?? [], catalogByCategory, "soft-skills"),
                RequiredDepartmentProcesses = SkillService.UnionWithCatalog(
                    JsonSerializer.Deserialize<List<string>>(jobRole.DepartmentProcessesJson) ?? [], catalogByCategory, "department-processes"),
                RequiredHrProcesses = SkillService.UnionWithCatalog(
                    JsonSerializer.Deserialize<List<string>>(jobRole.HrProcessesJson) ?? [], catalogByCategory, "hr-processes"),
                CurrentSkills = currentSkills,
                RealPrograms = realPrograms,
            },
            cancellationToken);

        // Defensive re-check: never trust the agent's MatchedProgramId
        // blindly — only keep it (and look up the real Program name from
        // our own query) when it points at a real, active Program we
        // actually handed it, same rationale as RecommendGrowthPathFunction.
        var realProgramsById = realPrograms.ToDictionary(p => p.ProgramId);

        List<CareerPlanRoadmapStepDto> ValidateRoadmap(List<CareerPlanRoadmapStep> steps) => steps
            .Select(s =>
            {
                var matched = s.MatchedProgramId.HasValue && realProgramsById.TryGetValue(s.MatchedProgramId.Value, out var program)
                    ? program
                    : null;

                return new CareerPlanRoadmapStepDto
                {
                    Order = s.Order,
                    Skill = s.Skill,
                    Title = matched?.Name ?? s.Title,
                    Description = s.Description,
                    MatchedProgramId = matched?.ProgramId,
                    MatchedProgramName = matched?.Name,
                };
            })
            .ToList();

        var technicalRoadmap = ValidateRoadmap(proposal.TechnicalSkillsRoadmap);
        var softSkillsRoadmap = ValidateRoadmap(proposal.SoftSkillsRoadmap);
        var departmentProcessesRoadmap = ValidateRoadmap(proposal.DepartmentProcessesRoadmap);
        var hrProcessesRoadmap = ValidateRoadmap(proposal.HrProcessesRoadmap);

        var now = DateTime.UtcNow;

        var plan = await _dbContext.PersonCareerPlans
            .SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);

        if (plan is null)
        {
            plan = new PersonCareerPlan
            {
                PersonCareerPlanId = Guid.NewGuid(),
                PersonId = personId,
                CreatedDate = now,
            };
            _dbContext.PersonCareerPlans.Add(plan);
        }

        plan.JobRoleId = jobRole.JobRoleId;
        plan.GapSkillsJson = JsonSerializer.Serialize(proposal.GapSkills);
        plan.TechnicalRoadmapJson = JsonSerializer.Serialize(technicalRoadmap);
        plan.SoftSkillsRoadmapJson = JsonSerializer.Serialize(softSkillsRoadmap);
        plan.DepartmentProcessesRoadmapJson = JsonSerializer.Serialize(departmentProcessesRoadmap);
        plan.HrProcessesRoadmapJson = JsonSerializer.Serialize(hrProcessesRoadmap);
        plan.OverallRationale = string.IsNullOrWhiteSpace(proposal.OverallRationale) ? null : proposal.OverallRationale.Trim();
        plan.GeneratedModel = _agent.DeploymentName;
        plan.GeneratedDate = now;
        plan.UpdatedDate = now;

        // A freshly (re)generated plan is a new AI proposal — any prior
        // HR acceptance no longer applies to it and must be re-confirmed.
        plan.IsAccepted = false;
        plan.AcceptedDate = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var generatedResponse = ToResponse(plan, jobRole.Title);
        await ApplyTrainingStatusAsync(generatedResponse, personId, cancellationToken);
        return generatedResponse;
    }

    /// <summary>Automatically detects, for every roadmap step that has no
    /// matched course yet, whether a Capability now exists in the catalog
    /// that closes it — e.g. HR just generated it in Capability Studio for
    /// a gap that previously had nothing. No human ever links anything for
    /// the fallback path: this runs on every plan read (GetByPersonIdAsync).
    /// Two ways a Capability can match a step, tried in order:
    /// (1) EXPLICIT: HR tagged the Capability (in Capability Studio) with
    /// a TargetSkill equal (case-insensitive) to the step's Skill, in the
    /// same Category (or no Category set) — set deliberately by a human
    /// picking the skill straight from the shared Skill catalog, so this
    /// always wins and ignores Name/Title entirely. Matches ANY job role
    /// that requires that skill (the catalog is already the source of
    /// truth for which roles need it — see SkillService/JobRoleSkill), not
    /// just one specific role. If TargetJobRoleId also happens to be set
    /// (older data from before the job-role picker was removed from the
    /// wizard), it must additionally match this plan's JobRoleId.
    /// (2) FALLBACK (legacy behavior): a standalone Capability whose
    /// Category matches this track (or has no Category set, for backward
    /// compatibility with capabilities created before this field existed)
    /// and whose Name exactly matches the step's Title. When a match is
    /// found the employee is immediately auto-enrolled in that Capability
    /// (so it shows up in Engram's "Mis Capacidades" right away) and the
    /// match is persisted onto the step so this lookup is a cheap no-op
    /// afterward. ALSO revalidates already-matched steps: if the
    /// previously matched Capability was since deleted or deactivated in
    /// Studio, the stale MatchedCapabilityId/Name is cleared so the step
    /// falls back to "Generar" (or gets re-matched to a replacement
    /// Capability, if one with the same name exists) instead of forever
    /// pointing at a course that no longer exists.</summary>
    private async Task AutoMatchCapabilitiesAsync(
        PersonCareerPlan plan,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var technicalSteps = JsonSerializer.Deserialize<List<CareerPlanRoadmapStepDto>>(plan.TechnicalRoadmapJson) ?? [];
        var softSteps = JsonSerializer.Deserialize<List<CareerPlanRoadmapStepDto>>(plan.SoftSkillsRoadmapJson) ?? [];
        var departmentProcessSteps = JsonSerializer.Deserialize<List<CareerPlanRoadmapStepDto>>(plan.DepartmentProcessesRoadmapJson) ?? [];
        var hrProcessSteps = JsonSerializer.Deserialize<List<CareerPlanRoadmapStepDto>>(plan.HrProcessesRoadmapJson) ?? [];

        var tracks = new (List<CareerPlanRoadmapStepDto> Steps, string Category)[]
        {
            (technicalSteps, "job-skills"),
            (softSteps, "soft-skills"),
            (departmentProcessSteps, "department-processes"),
            (hrProcessSteps, "hr-processes"),
        };

        // Also re-run whenever some step already has a MatchedCapabilityId,
        // so a match that was later deleted/deactivated in Studio can be
        // detected and cleared below (2026-08-07 bug: a dangling match kept
        // showing a "Tomar" button for a course that no longer exists —
        // this method previously only ever looked at UNMATCHED steps, so a
        // stale MatchedCapabilityId/MatchedCapabilityName was never
        // revisited once set).
        var hasUnmatched = tracks.Any(t => t.Steps.Any(s => s.MatchedProgramId is null && s.MatchedCapabilityId is null));
        var hasCapabilityMatch = tracks.Any(t => t.Steps.Any(s => s.MatchedCapabilityId is not null));

        if (!hasUnmatched && !hasCapabilityMatch)
        {
            return;
        }

        var activeCapabilities = await _dbContext.Capabilities
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.CapabilityId, c.Name, c.Category, c.TargetJobRoleId, c.TargetSkill })
            .ToListAsync(cancellationToken);
        var activeCapabilityIds = activeCapabilities.Select(c => c.CapabilityId).ToHashSet();

        var changed = false;

        foreach (var (steps, category) in tracks)
        {
            foreach (var step in steps)
            {
                if (step.MatchedCapabilityId is not null && !activeCapabilityIds.Contains(step.MatchedCapabilityId.Value))
                {
                    step.MatchedCapabilityId = null;
                    step.MatchedCapabilityName = null;
                    changed = true;
                }

                if (step.MatchedProgramId is not null || step.MatchedCapabilityId is not null)
                {
                    continue;
                }

                var explicitMatch = activeCapabilities.FirstOrDefault(
                    c => c.TargetSkill is not null
                        && (c.Category is null || c.Category == category)
                        && string.Equals(c.TargetSkill.Trim(), step.Skill.Trim(), StringComparison.OrdinalIgnoreCase)
                        && (c.TargetJobRoleId is null || c.TargetJobRoleId == plan.JobRoleId));

                var match = explicitMatch ?? activeCapabilities.FirstOrDefault(
                    c => (c.Category is null || c.Category == category)
                        && string.Equals(c.Name.Trim(), step.Title.Trim(), StringComparison.OrdinalIgnoreCase));

                if (match is null)
                {
                    continue;
                }

                step.MatchedCapabilityId = match.CapabilityId;
                step.MatchedCapabilityName = match.Name;
                changed = true;

                await _capabilityEnrollmentService.EnrollAsync(personId, match.CapabilityId, cancellationToken);
            }
        }

        if (!changed)
        {
            return;
        }

        plan.TechnicalRoadmapJson = JsonSerializer.Serialize(technicalSteps);
        plan.SoftSkillsRoadmapJson = JsonSerializer.Serialize(softSteps);
        plan.DepartmentProcessesRoadmapJson = JsonSerializer.Serialize(departmentProcessSteps);
        plan.HrProcessesRoadmapJson = JsonSerializer.Serialize(hrProcessSteps);
        plan.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Marks the person's current saved plan as accepted by HR —
    /// i.e. confirmed as the employee's official training plan. Does not
    /// change any of the plan's content, only its acceptance state.</summary>
    public async Task<PersonCareerPlanResponse> AcceptAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {        var plan = await _dbContext.PersonCareerPlans
            .SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken)
            ?? throw new KeyNotFoundException("This employee has no career plan generated yet.");

        var now = DateTime.UtcNow;
        plan.IsAccepted = true;
        plan.AcceptedDate = now;
        plan.UpdatedDate = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var jobRoleTitle = await _dbContext.JobRoles
            .AsNoTracking()
            .Where(r => r.JobRoleId == plan.JobRoleId)
            .Select(r => r.Title)
            .SingleOrDefaultAsync(cancellationToken);

        var acceptResponse = ToResponse(plan, jobRoleTitle);
        await ApplyTrainingStatusAsync(acceptResponse, personId, cancellationToken);
        return acceptResponse;
    }

    /// <summary>Fills in each roadmap step's real TrainingStatus by cross-
    /// referencing this person's enrolled Programs (Growth Plan's "Mis
    /// Programas" data source) against each step's MatchedProgramId, and
    /// their enrolled standalone Capabilities against MatchedCapabilityId
    /// — the SAME Capability Graph progress used across the whole product,
    /// never a fabricated/estimated status. Steps with no match, or whose
    /// matched course this person hasn't enrolled in, stay at the DTO
    /// default "Pending".</summary>
    private async Task ApplyTrainingStatusAsync(
        PersonCareerPlanResponse response,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var allSteps = response.TechnicalRoadmap
            .Concat(response.SoftSkillsRoadmap)
            .Concat(response.DepartmentProcessesRoadmap)
            .Concat(response.HrProcessesRoadmap)
            .ToList();

        if (allSteps.Any(s => s.MatchedProgramId.HasValue))
        {
            var enrolledPrograms = await _growthPlanService.GetEnrolledProgramsAsync(personId, cancellationToken);
            var statusByProgramId = enrolledPrograms.ToDictionary(p => p.ProgramId, DeriveProgramStatus);

            foreach (var step in allSteps)
            {
                if (step.MatchedProgramId.HasValue && statusByProgramId.TryGetValue(step.MatchedProgramId.Value, out var status))
                {
                    step.TrainingStatus = status;
                }
            }
        }

        if (allSteps.Any(s => s.MatchedCapabilityId.HasValue))
        {
            var enrolledCapabilities = await _capabilityEnrollmentService.GetEnrolledCapabilitiesAsync(personId, cancellationToken);
            var statusByCapabilityId = enrolledCapabilities.ToDictionary(c => c.CapabilityId, DeriveCapabilityStatus);

            foreach (var step in allSteps)
            {
                if (step.MatchedCapabilityId.HasValue && statusByCapabilityId.TryGetValue(step.MatchedCapabilityId.Value, out var status))
                {
                    step.TrainingStatus = status;
                }
            }
        }
    }

    /// <summary>Maps EnrolledCapabilityResponse.Status ("NotStarted" |
    /// "InProgress" | "Completed") onto the roadmap's own vocabulary
    /// ("Pending" | "InProgress" | "Completed").</summary>
    private static string DeriveCapabilityStatus(HumanOS.Contracts.Capabilities.EnrolledCapabilityResponse capability) =>
        capability.Status == "NotStarted" ? "Pending" : capability.Status;

    /// <summary>"Completed" only once every Capability in the Program is
    /// mastered; "InProgress" once at least one Capability has been
    /// started or completed; otherwise "Pending" (enrolled but not started,
    /// or a Program with no Capabilities yet).</summary>
    private static string DeriveProgramStatus(HumanOS.Contracts.GrowthPlan.EnrolledProgramResponse program)
    {
        if (program.Capabilities.Count == 0)
        {
            return "Pending";
        }

        if (program.Capabilities.All(c => c.Status == "Completed"))
        {
            return "Completed";
        }

        if (program.Capabilities.Any(c => c.Status is "InProgress" or "Completed"))
        {
            return "InProgress";
        }

        return "Pending";
    }

    private static PersonCareerPlanResponse ToResponse(PersonCareerPlan plan, string? jobRoleTitle) => new()
    {
        PersonId = plan.PersonId,
        HasPlan = true,
        JobRoleId = plan.JobRoleId,
        JobRoleTitle = jobRoleTitle,
        GapSkills = JsonSerializer.Deserialize<List<string>>(plan.GapSkillsJson) ?? [],
        TechnicalRoadmap = JsonSerializer.Deserialize<List<CareerPlanRoadmapStepDto>>(plan.TechnicalRoadmapJson) ?? [],
        SoftSkillsRoadmap = JsonSerializer.Deserialize<List<CareerPlanRoadmapStepDto>>(plan.SoftSkillsRoadmapJson) ?? [],
        DepartmentProcessesRoadmap = JsonSerializer.Deserialize<List<CareerPlanRoadmapStepDto>>(plan.DepartmentProcessesRoadmapJson) ?? [],
        HrProcessesRoadmap = JsonSerializer.Deserialize<List<CareerPlanRoadmapStepDto>>(plan.HrProcessesRoadmapJson) ?? [],
        OverallRationale = plan.OverallRationale,
        GeneratedDate = plan.GeneratedDate,
        IsAccepted = plan.IsAccepted,
        AcceptedDate = plan.AcceptedDate,
    };
}
