using HumanOS.Contracts.OrgStructure;
using HumanOS.Data;
using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>CRUD for a Tenant's organizational chart (Área/Departamento/
/// Equipo — all the same self-referencing <see cref="Department"/> node).
/// Every read/write is scoped to a single TenantId; callers (Functions)
/// are responsible for resolving that TenantId from the signed-in
/// HRAdmin's session.</summary>
public sealed class DepartmentService
{
    private readonly HumanOsDbContext _dbContext;

    public DepartmentService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Every Department for a tenant (flat list — the frontend
    /// builds the tree client-side from ParentDepartmentId), including
    /// JobRole/Person counts for the org-chart admin screen.</summary>
    public async Task<IReadOnlyList<DepartmentResponse>> GetByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var departments = await _dbContext.Departments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .Select(d => new
            {
                Department = d,
                JobRoleCount = d.DepartmentJobRoles.Count > 0 ? d.DepartmentJobRoles.Count : d.JobRoles.Count,
                PersonCount = _dbContext.People.Count(p => p.DepartmentId == d.DepartmentId),
            })
            .OrderBy(x => x.Department.Name)
            .ToListAsync(cancellationToken);

        var leaderIds = departments
            .Where(x => x.Department.LeaderPersonId.HasValue)
            .Select(x => x.Department.LeaderPersonId!.Value);

        var leaderNames = await ResolveLeaderNamesAsync(leaderIds, cancellationToken);

        return departments
            .Select(x => ToResponse(
                x.Department,
                x.JobRoleCount,
                x.PersonCount,
                x.Department.LeaderPersonId.HasValue && leaderNames.TryGetValue(x.Department.LeaderPersonId.Value, out var name)
                    ? name
                    : null))
            .ToList();
    }

    public async Task<Department?> GetByIdAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Departments
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.DepartmentId == departmentId, cancellationToken);
    }

    public async Task<DepartmentResponse?> GetResponseByIdAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.Departments
            .AsNoTracking()
            .Where(d => d.DepartmentId == departmentId)
            .Select(d => new
            {
                Department = d,
                JobRoleCount = d.DepartmentJobRoles.Count > 0 ? d.DepartmentJobRoles.Count : d.JobRoles.Count,
                PersonCount = _dbContext.People.Count(p => p.DepartmentId == d.DepartmentId),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null) return null;

        string? leaderName = null;
        if (row.Department.LeaderPersonId is Guid leaderId)
        {
            var leaderNames = await ResolveLeaderNamesAsync([leaderId], cancellationToken);
            leaderNames.TryGetValue(leaderId, out leaderName);
        }

        return ToResponse(row.Department, row.JobRoleCount, row.PersonCount, leaderName);
    }

    public async Task<DepartmentResponse> CreateAsync(
        Guid tenantId,
        SaveDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ParentDepartmentId is Guid parentId)
        {
            var parentExists = await _dbContext.Departments
                .AnyAsync(d => d.DepartmentId == parentId && d.TenantId == tenantId, cancellationToken);
            if (!parentExists)
            {
                throw new InvalidOperationException($"Parent department {parentId} does not belong to this tenant.");
            }
        }

        if (request.LeaderPersonId is Guid leaderId)
        {
            var leaderExists = await _dbContext.People
                .AnyAsync(p => p.PersonId == leaderId && p.TenantId == tenantId, cancellationToken);
            if (!leaderExists)
            {
                throw new InvalidOperationException($"Leader person {leaderId} does not belong to this tenant.");
            }
        }

        var now = DateTime.UtcNow;
        var department = new Department
        {
            DepartmentId = Guid.NewGuid(),
            TenantId = tenantId,
            ParentDepartmentId = request.ParentDepartmentId,
            Name = request.Name.Trim(),
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim(),
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            CostCenter = string.IsNullOrWhiteSpace(request.CostCenter) ? null : request.CostCenter.Trim(),
            MissionStatement = string.IsNullOrWhiteSpace(request.MissionStatement) ? null : request.MissionStatement.Trim(),
            LeaderPersonId = request.LeaderPersonId,
            IsActive = true,
            CreatedDate = now,
            UpdatedDate = now,
        };

        _dbContext.Departments.Add(department);
        await _dbContext.SaveChangesAsync(cancellationToken);

        string? leaderName = null;
        if (department.LeaderPersonId is Guid lId)
        {
            var leaderNames = await ResolveLeaderNamesAsync([lId], cancellationToken);
            leaderNames.TryGetValue(lId, out leaderName);
        }

        return ToResponse(department, jobRoleCount: 0, personCount: 0, leaderName);
    }

    public async Task<DepartmentResponse?> UpdateAsync(
        Guid departmentId,
        SaveDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var department = await _dbContext.Departments
            .SingleOrDefaultAsync(d => d.DepartmentId == departmentId, cancellationToken);

        if (department is null)
        {
            return null;
        }

        if (request.ParentDepartmentId == departmentId)
        {
            throw new InvalidOperationException("A department cannot be its own parent.");
        }

        if (request.ParentDepartmentId is Guid parentId)
        {
            var parentExists = await _dbContext.Departments
                .AnyAsync(d => d.DepartmentId == parentId && d.TenantId == department.TenantId, cancellationToken);
            if (!parentExists)
            {
                throw new InvalidOperationException($"Parent department {parentId} does not belong to this tenant.");
            }
        }

        if (request.LeaderPersonId is Guid leaderId)
        {
            var leaderExists = await _dbContext.People
                .AnyAsync(p => p.PersonId == leaderId && p.TenantId == department.TenantId, cancellationToken);
            if (!leaderExists)
            {
                throw new InvalidOperationException($"Leader person {leaderId} does not belong to this tenant.");
            }
        }

        department.Name = request.Name.Trim();
        department.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        department.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        department.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        department.ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim();
        department.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        department.CostCenter = string.IsNullOrWhiteSpace(request.CostCenter) ? null : request.CostCenter.Trim();
        department.MissionStatement = string.IsNullOrWhiteSpace(request.MissionStatement) ? null : request.MissionStatement.Trim();
        department.LeaderPersonId = request.LeaderPersonId;
        department.ParentDepartmentId = request.ParentDepartmentId;
        department.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var jobRoleCount = await _dbContext.JobRoles.CountAsync(r => r.DepartmentId == departmentId, cancellationToken);
        var personCount = await _dbContext.People.CountAsync(p => p.DepartmentId == departmentId, cancellationToken);

        string? leaderName = null;
        if (department.LeaderPersonId is Guid lId)
        {
            var leaderNames = await ResolveLeaderNamesAsync([lId], cancellationToken);
            leaderNames.TryGetValue(lId, out leaderName);
        }

        return ToResponse(department, jobRoleCount, personCount, leaderName);
    }

    /// <summary>Soft-deletes (deactivates) a Department. Refuses if it
    /// still has active child departments, job roles, or assigned people —
    /// the caller must reassign/deactivate those first (mapped to 409 by
    /// the calling Function).</summary>
    public async Task<bool> DeactivateAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        var department = await _dbContext.Departments
            .SingleOrDefaultAsync(d => d.DepartmentId == departmentId, cancellationToken);

        if (department is null)
        {
            return false;
        }

        var hasChildren = await _dbContext.Departments
            .AnyAsync(d => d.ParentDepartmentId == departmentId && d.IsActive, cancellationToken);
        var hasJobRoles = await _dbContext.JobRoles
            .AnyAsync(r => r.DepartmentId == departmentId && r.IsActive, cancellationToken);
        var hasPeople = await _dbContext.People
            .AnyAsync(p => p.DepartmentId == departmentId && p.IsActive, cancellationToken);

        if (hasChildren || hasJobRoles || hasPeople)
        {
            throw new InvalidOperationException(
                "Cannot deactivate a department that still has active sub-departments, job roles, or assigned people.");
        }

        department.IsActive = false;
        department.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<Dictionary<Guid, string>> ResolveLeaderNamesAsync(
        IEnumerable<Guid> leaderPersonIds,
        CancellationToken cancellationToken)
    {
        var ids = leaderPersonIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var profiles = await _dbContext.PersonProfiles
            .AsNoTracking()
            .Where(pp => ids.Contains(pp.PersonId))
            .ToDictionaryAsync(pp => pp.PersonId, cancellationToken);

        var people = await _dbContext.People
            .AsNoTracking()
            .Where(p => ids.Contains(p.PersonId))
            .ToDictionaryAsync(p => p.PersonId, cancellationToken);

        var dict = new Dictionary<Guid, string>();
        foreach (var id in ids)
        {
            if (profiles.TryGetValue(id, out var profile) && !string.IsNullOrWhiteSpace(profile.DisplayName))
            {
                dict[id] = profile.DisplayName;
            }
            else if (profile != null && (!string.IsNullOrWhiteSpace(profile.FirstName) || !string.IsNullOrWhiteSpace(profile.LastName)))
            {
                dict[id] = $"{profile.FirstName} {profile.LastName}".Trim();
            }
            else if (people.TryGetValue(id, out var person) && !string.IsNullOrWhiteSpace(person.Email))
            {
                dict[id] = person.Email;
            }
        }

        return dict;
    }

    private static DepartmentResponse ToResponse(
        Department department,
        int jobRoleCount,
        int personCount,
        string? leaderDisplayName = null) => new()
    {
        DepartmentId = department.DepartmentId,
        TenantId = department.TenantId,
        ParentDepartmentId = department.ParentDepartmentId,
        Name = department.Name,
        Code = department.Code,
        Description = department.Description,
        Category = department.Category,
        ContactEmail = department.ContactEmail,
        Location = department.Location,
        CostCenter = department.CostCenter,
        MissionStatement = department.MissionStatement,
        LeaderPersonId = department.LeaderPersonId,
        LeaderDisplayName = leaderDisplayName,
        IsActive = department.IsActive,
        JobRoleCount = jobRoleCount,
        PersonCount = personCount,
        CreatedDate = department.CreatedDate,
        UpdatedDate = department.UpdatedDate,
    };
}
