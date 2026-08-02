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
        return await _dbContext.Departments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .Select(d => new DepartmentResponse
            {
                DepartmentId = d.DepartmentId,
                TenantId = d.TenantId,
                ParentDepartmentId = d.ParentDepartmentId,
                Name = d.Name,
                Description = d.Description,
                IsActive = d.IsActive,
                JobRoleCount = d.JobRoles.Count,
                PersonCount = _dbContext.People.Count(p => p.DepartmentId == d.DepartmentId),
                CreatedDate = d.CreatedDate,
                UpdatedDate = d.UpdatedDate,
            })
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Department?> GetByIdAsync(
        Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Departments
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.DepartmentId == departmentId, cancellationToken);
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

        var now = DateTime.UtcNow;
        var department = new Department
        {
            DepartmentId = Guid.NewGuid(),
            TenantId = tenantId,
            ParentDepartmentId = request.ParentDepartmentId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
            CreatedDate = now,
            UpdatedDate = now,
        };

        _dbContext.Departments.Add(department);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(department, jobRoleCount: 0, personCount: 0);
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

        department.Name = request.Name.Trim();
        department.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        department.ParentDepartmentId = request.ParentDepartmentId;
        department.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var jobRoleCount = await _dbContext.JobRoles.CountAsync(r => r.DepartmentId == departmentId, cancellationToken);
        var personCount = await _dbContext.People.CountAsync(p => p.DepartmentId == departmentId, cancellationToken);

        return ToResponse(department, jobRoleCount, personCount);
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

    private static DepartmentResponse ToResponse(Department department, int jobRoleCount, int personCount) => new()
    {
        DepartmentId = department.DepartmentId,
        TenantId = department.TenantId,
        ParentDepartmentId = department.ParentDepartmentId,
        Name = department.Name,
        Description = department.Description,
        IsActive = department.IsActive,
        JobRoleCount = jobRoleCount,
        PersonCount = personCount,
        CreatedDate = department.CreatedDate,
        UpdatedDate = department.UpdatedDate,
    };
}
