using System.Text.Json;
using HumanOS.Contracts.OrgStructure;
using HumanOS.Data;
using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>CRUD for a Tenant's Job Roles ("puestos") — org-authored
/// position templates attached to a Department, distinct from the
/// per-person <see cref="HumanOS.Models.JobDescriptions.JobDescriptionRecord"/>
/// (see JobRole's own doc comment). Role↔Capability training
/// requirements/certifications and career roadmaps are later phases, not
/// handled here yet.</summary>
public sealed class JobRoleService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly SkillService _skillService;

    public JobRoleService(HumanOsDbContext dbContext, SkillService skillService)
    {
        _dbContext = dbContext;
        _skillService = skillService;
    }

    public async Task<IReadOnlyList<JobRoleResponse>> GetByTenantAsync(
        Guid tenantId,
        Guid? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.JobRoles
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.IsActive);

        if (departmentId is Guid deptId)
        {
            query = query.Where(r => r.DepartmentId == deptId);
        }

        var roles = await query
            .Select(r => new
            {
                Role = r,
                DepartmentName = r.Department.Name,
                PersonCount = _dbContext.People.Count(p => p.JobRoleId == r.JobRoleId),
                ReportsToTitle = r.ReportsToJobRole != null ? r.ReportsToJobRole.Title : null,
                HasDirectReports = _dbContext.JobRoles.Any(x => x.ReportsToJobRoleId == r.JobRoleId),
            })
            .OrderBy(x => x.DepartmentName)
            .ThenBy(x => x.Role.Title)
            .ToListAsync(cancellationToken);

        var catalogByRole = await SkillService.GetCatalogNamesByRolesAsync(
            _dbContext, roles.Select(x => x.Role.JobRoleId), cancellationToken);

        return roles
            .Select(x => ToResponse(
                x.Role, x.DepartmentName, x.PersonCount, x.ReportsToTitle, x.HasDirectReports,
                catalogByRole.GetValueOrDefault(x.Role.JobRoleId)))
            .ToList();
    }

    public async Task<JobRole?> GetByIdAsync(
        Guid jobRoleId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.JobRoles
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.JobRoleId == jobRoleId, cancellationToken);
    }

    /// <summary>Same shape as the list endpoint's items — used by the
    /// Roles y Puestos full-page editor to load one role with its
    /// resolved DepartmentName/ReportsToJobRoleTitle/HasDirectReports.</summary>
    public async Task<JobRoleResponse?> GetResponseByIdAsync(
        Guid jobRoleId,
        CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.JobRoles
            .AsNoTracking()
            .Where(r => r.JobRoleId == jobRoleId)
            .Select(r => new
            {
                Role = r,
                DepartmentName = r.Department.Name,
                PersonCount = _dbContext.People.Count(p => p.JobRoleId == r.JobRoleId),
                ReportsToTitle = r.ReportsToJobRole != null ? r.ReportsToJobRole.Title : null,
                HasDirectReports = _dbContext.JobRoles.Any(x => x.ReportsToJobRoleId == r.JobRoleId),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var catalog = await SkillService.GetCatalogNamesByCategoryAsync(_dbContext, row.Role.JobRoleId, cancellationToken);
        return ToResponse(row.Role, row.DepartmentName, row.PersonCount, row.ReportsToTitle, row.HasDirectReports, catalog);
    }

    public async Task<JobRoleResponse> CreateAsync(
        Guid tenantId,
        SaveJobRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var department = await _dbContext.Departments
            .SingleOrDefaultAsync(d => d.DepartmentId == request.DepartmentId && d.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Department {request.DepartmentId} does not belong to this tenant.");

        await ValidateReportsToAsync(tenantId, request.ReportsToJobRoleId, currentJobRoleId: null, cancellationToken);

        var now = DateTime.UtcNow;
        var jobRole = new JobRole
        {
            JobRoleId = Guid.NewGuid(),
            TenantId = tenantId,
            IsActive = true,
            CreatedDate = now,
        };

        ApplyRequest(jobRole, request, now);

        _dbContext.JobRoles.Add(jobRole);
        await _skillService.ReconcileForJobRoleAsync(jobRole, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var reportsToTitle = await ResolveReportsToTitleAsync(jobRole.ReportsToJobRoleId, cancellationToken);
        var catalog = await SkillService.GetCatalogNamesByCategoryAsync(_dbContext, jobRole.JobRoleId, cancellationToken);
        return ToResponse(jobRole, department.Name, personCount: 0, reportsToTitle, hasDirectReports: false, catalog);
    }

    public async Task<JobRoleResponse?> UpdateAsync(
        Guid jobRoleId,
        SaveJobRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var jobRole = await _dbContext.JobRoles
            .SingleOrDefaultAsync(r => r.JobRoleId == jobRoleId, cancellationToken);

        if (jobRole is null)
        {
            return null;
        }

        var department = await _dbContext.Departments
            .SingleOrDefaultAsync(d => d.DepartmentId == request.DepartmentId && d.TenantId == jobRole.TenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Department {request.DepartmentId} does not belong to this tenant.");

        await ValidateReportsToAsync(jobRole.TenantId, request.ReportsToJobRoleId, currentJobRoleId: jobRoleId, cancellationToken);

        ApplyRequest(jobRole, request, DateTime.UtcNow);

        await _skillService.ReconcileForJobRoleAsync(jobRole, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var personCount = await _dbContext.People.CountAsync(p => p.JobRoleId == jobRoleId, cancellationToken);
        var reportsToTitle = await ResolveReportsToTitleAsync(jobRole.ReportsToJobRoleId, cancellationToken);
        var hasDirectReports = await _dbContext.JobRoles.AnyAsync(r => r.ReportsToJobRoleId == jobRoleId, cancellationToken);
        var catalog = await SkillService.GetCatalogNamesByCategoryAsync(_dbContext, jobRoleId, cancellationToken);

        return ToResponse(jobRole, department.Name, personCount, reportsToTitle, hasDirectReports, catalog);
    }

    /// <summary>Copies every editable field from the request onto the
    /// tracked entity — shared by CreateAsync/UpdateAsync so the two
    /// never drift apart.</summary>
    private static void ApplyRequest(JobRole jobRole, SaveJobRoleRequest request, DateTime now)
    {
        jobRole.DepartmentId = request.DepartmentId;
        jobRole.Title = request.Title.Trim();
        jobRole.LevelLabel = string.IsNullOrWhiteSpace(request.LevelLabel) ? null : request.LevelLabel.Trim();
        jobRole.Purpose = string.IsNullOrWhiteSpace(request.Purpose) ? null : request.Purpose.Trim();
        jobRole.Summary = string.IsNullOrWhiteSpace(request.Summary) ? null : request.Summary.Trim();
        jobRole.ResponsibilitiesJson = JsonSerializer.Serialize(request.Responsibilities ?? []);
        jobRole.RequiredExperience = string.IsNullOrWhiteSpace(request.RequiredExperience) ? null : request.RequiredExperience.Trim();
        jobRole.RequiredEducation = string.IsNullOrWhiteSpace(request.RequiredEducation) ? null : request.RequiredEducation.Trim();
        jobRole.RequiredCertificationsJson = JsonSerializer.Serialize(request.RequiredCertifications ?? []);
        jobRole.RequiredLanguagesJson = JsonSerializer.Serialize(request.RequiredLanguages ?? []);
        jobRole.TechnicalSkillsJson = JsonSerializer.Serialize(request.TechnicalSkills ?? []);
        jobRole.SoftSkillsJson = JsonSerializer.Serialize(request.SoftSkills ?? []);
        jobRole.DepartmentProcessesJson = JsonSerializer.Serialize(request.DepartmentProcesses ?? []);
        jobRole.HrProcessesJson = JsonSerializer.Serialize(request.HrProcesses ?? []);
        jobRole.ToolsJson = JsonSerializer.Serialize(request.Tools ?? []);
        jobRole.DailyActivitiesJson = JsonSerializer.Serialize(request.DailyActivities ?? []);
        jobRole.PerformanceIndicatorsJson = JsonSerializer.Serialize(request.PerformanceIndicators ?? []);
        jobRole.ExpectedOutcomesJson = JsonSerializer.Serialize(request.ExpectedOutcomes ?? []);
        jobRole.ReportsToJobRoleId = request.ReportsToJobRoleId;
        jobRole.WorkSchedule = string.IsNullOrWhiteSpace(request.WorkSchedule) ? null : request.WorkSchedule.Trim();
        jobRole.WorkModality = string.IsNullOrWhiteSpace(request.WorkModality) ? null : request.WorkModality.Trim();
        jobRole.RequiresTravel = request.RequiresTravel;
        jobRole.WorkLocation = string.IsNullOrWhiteSpace(request.WorkLocation) ? null : request.WorkLocation.Trim();
        jobRole.AvailabilityNotes = string.IsNullOrWhiteSpace(request.AvailabilityNotes) ? null : request.AvailabilityNotes.Trim();
        jobRole.EmploymentType = string.IsNullOrWhiteSpace(request.EmploymentType) ? null : request.EmploymentType.Trim();
        jobRole.SalaryRangeMin = request.SalaryRangeMin;
        jobRole.SalaryRangeMax = request.SalaryRangeMax;
        jobRole.SalaryCurrency = string.IsNullOrWhiteSpace(request.SalaryCurrency) ? null : request.SalaryCurrency.Trim();
        jobRole.BenefitsJson = JsonSerializer.Serialize(request.Benefits ?? []);
        jobRole.UpdatedDate = now;
    }

    /// <summary>Ensures a "reports to" pointer (when provided) belongs to
    /// the same tenant and isn't the role pointing to itself.</summary>
    private async Task ValidateReportsToAsync(
        Guid tenantId,
        Guid? reportsToJobRoleId,
        Guid? currentJobRoleId,
        CancellationToken cancellationToken)
    {
        if (reportsToJobRoleId is not Guid reportsToId)
        {
            return;
        }

        if (currentJobRoleId is Guid current && reportsToId == current)
        {
            throw new InvalidOperationException("A job role cannot report to itself.");
        }

        var exists = await _dbContext.JobRoles
            .AnyAsync(r => r.JobRoleId == reportsToId && r.TenantId == tenantId, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException($"JobRole {reportsToId} does not belong to this tenant.");
        }
    }

    private async Task<string?> ResolveReportsToTitleAsync(Guid? reportsToJobRoleId, CancellationToken cancellationToken)
    {
        if (reportsToJobRoleId is not Guid reportsToId)
        {
            return null;
        }

        return await _dbContext.JobRoles
            .Where(r => r.JobRoleId == reportsToId)
            .Select(r => r.Title)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Soft-deletes (deactivates) a Job Role. Refuses if people
    /// are still assigned to it (mapped to 409 by the calling Function).</summary>
    public async Task<bool> DeactivateAsync(
        Guid jobRoleId,
        CancellationToken cancellationToken = default)
    {
        var jobRole = await _dbContext.JobRoles
            .SingleOrDefaultAsync(r => r.JobRoleId == jobRoleId, cancellationToken);

        if (jobRole is null)
        {
            return false;
        }

        var hasPeople = await _dbContext.People
            .AnyAsync(p => p.JobRoleId == jobRoleId && p.IsActive, cancellationToken);

        if (hasPeople)
        {
            throw new InvalidOperationException("Cannot deactivate a job role that still has people assigned to it.");
        }

        jobRole.IsActive = false;
        jobRole.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static JobRoleResponse ToResponse(
        JobRole jobRole,
        string departmentName,
        int personCount,
        string? reportsToTitle,
        bool hasDirectReports,
        Dictionary<string, List<string>>? catalogByCategory = null) => new()
    {
        JobRoleId = jobRole.JobRoleId,
        TenantId = jobRole.TenantId,
        DepartmentId = jobRole.DepartmentId,
        DepartmentName = departmentName,
        Title = jobRole.Title,
        LevelLabel = jobRole.LevelLabel,
        Purpose = jobRole.Purpose,
        Summary = jobRole.Summary,
        Responsibilities = JsonSerializer.Deserialize<List<string>>(jobRole.ResponsibilitiesJson) ?? [],
        RequiredExperience = jobRole.RequiredExperience,
        RequiredEducation = jobRole.RequiredEducation,
        RequiredCertifications = JsonSerializer.Deserialize<List<string>>(jobRole.RequiredCertificationsJson) ?? [],
        RequiredLanguages = JsonSerializer.Deserialize<List<string>>(jobRole.RequiredLanguagesJson) ?? [],
        TechnicalSkills = SkillService.UnionWithCatalog(
            JsonSerializer.Deserialize<List<string>>(jobRole.TechnicalSkillsJson) ?? [], catalogByCategory, "job-skills"),
        SoftSkills = SkillService.UnionWithCatalog(
            JsonSerializer.Deserialize<List<string>>(jobRole.SoftSkillsJson) ?? [], catalogByCategory, "soft-skills"),
        DepartmentProcesses = SkillService.UnionWithCatalog(
            JsonSerializer.Deserialize<List<string>>(jobRole.DepartmentProcessesJson) ?? [], catalogByCategory, "department-processes"),
        HrProcesses = SkillService.UnionWithCatalog(
            JsonSerializer.Deserialize<List<string>>(jobRole.HrProcessesJson) ?? [], catalogByCategory, "hr-processes"),
        Tools = JsonSerializer.Deserialize<List<string>>(jobRole.ToolsJson) ?? [],
        DailyActivities = JsonSerializer.Deserialize<List<string>>(jobRole.DailyActivitiesJson) ?? [],
        PerformanceIndicators = JsonSerializer.Deserialize<List<string>>(jobRole.PerformanceIndicatorsJson) ?? [],
        ExpectedOutcomes = JsonSerializer.Deserialize<List<string>>(jobRole.ExpectedOutcomesJson) ?? [],
        ReportsToJobRoleId = jobRole.ReportsToJobRoleId,
        ReportsToJobRoleTitle = reportsToTitle,
        HasDirectReports = hasDirectReports,
        WorkSchedule = jobRole.WorkSchedule,
        WorkModality = jobRole.WorkModality,
        RequiresTravel = jobRole.RequiresTravel,
        WorkLocation = jobRole.WorkLocation,
        AvailabilityNotes = jobRole.AvailabilityNotes,
        EmploymentType = jobRole.EmploymentType,
        SalaryRangeMin = jobRole.SalaryRangeMin,
        SalaryRangeMax = jobRole.SalaryRangeMax,
        SalaryCurrency = jobRole.SalaryCurrency,
        Benefits = JsonSerializer.Deserialize<List<string>>(jobRole.BenefitsJson) ?? [],
        IsActive = jobRole.IsActive,
        PersonCount = personCount,
        CreatedDate = jobRole.CreatedDate,
        UpdatedDate = jobRole.UpdatedDate,
    };
}
