using HumanOS.Contracts.OrgStructure;
using HumanOS.Data;
using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>CRUD for a Tenant's Key Responsibility catalog — reusable
/// responsibilities shareable across one or many Job Roles.</summary>
public sealed class ResponsibilityService
{
    private readonly HumanOsDbContext _dbContext;

    public ResponsibilityService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ResponsibilityResponse>> GetByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var responsibilities = await _dbContext.Responsibilities
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var responsibilityIds = responsibilities.Select(r => r.ResponsibilityId).ToList();

        var links = await _dbContext.JobRoleResponsibilities
            .AsNoTracking()
            .Where(jrr => responsibilityIds.Contains(jrr.ResponsibilityId))
            .Select(jrr => new
            {
                jrr.ResponsibilityId,
                jrr.JobRoleId,
                JobRoleTitle = jrr.JobRole.Title,
                DepartmentName = jrr.JobRole.Department != null ? jrr.JobRole.Department.Name : "General",
            })
            .ToListAsync(cancellationToken);

        var linksByResp = links
            .GroupBy(l => l.ResponsibilityId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ResponsibilityJobRoleLinkResponse>)g
                    .Select(l => new ResponsibilityJobRoleLinkResponse
                    {
                        JobRoleId = l.JobRoleId,
                        JobRoleTitle = l.JobRoleTitle,
                        DepartmentName = l.DepartmentName,
                    })
                    .OrderBy(l => l.JobRoleTitle)
                    .ToList());

        return responsibilities
            .Select(r => ToResponse(r, linksByResp.GetValueOrDefault(r.ResponsibilityId, [])))
            .ToList();
    }

    public async Task<ResponsibilityResponse?> GetByIdAsync(
        Guid responsibilityId,
        CancellationToken cancellationToken = default)
    {
        var resp = await _dbContext.Responsibilities
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.ResponsibilityId == responsibilityId && r.IsActive, cancellationToken);

        if (resp is null)
        {
            return null;
        }

        var links = await _dbContext.JobRoleResponsibilities
            .AsNoTracking()
            .Where(jrr => jrr.ResponsibilityId == responsibilityId)
            .Select(jrr => new ResponsibilityJobRoleLinkResponse
            {
                JobRoleId = jrr.JobRoleId,
                JobRoleTitle = jrr.JobRole.Title,
                DepartmentName = jrr.JobRole.Department != null ? jrr.JobRole.Department.Name : "General",
            })
            .OrderBy(l => l.JobRoleTitle)
            .ToListAsync(cancellationToken);

        return ToResponse(resp, links);
    }

    public async Task<ResponsibilityResponse> CreateAsync(
        Guid tenantId,
        SaveResponsibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = ValidateName(request.Name);

        var duplicate = await _dbContext.Responsibilities.AnyAsync(
            r => r.TenantId == tenantId && r.IsActive && r.Name.ToLower() == name.ToLower(),
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"A responsibility named \"{name}\" already exists.");
        }

        var jobRoleIds = await ValidateJobRoleIdsAsync(tenantId, request.JobRoleIds, cancellationToken);

        var now = DateTime.UtcNow;
        var resp = new Responsibility
        {
            ResponsibilityId = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
            CreatedDate = now,
            UpdatedDate = now,
        };
        _dbContext.Responsibilities.Add(resp);

        foreach (var jobRoleId in jobRoleIds)
        {
            _dbContext.JobRoleResponsibilities.Add(new JobRoleResponsibility
            {
                JobRoleId = jobRoleId,
                ResponsibilityId = resp.ResponsibilityId,
                CreatedDate = now,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(resp.ResponsibilityId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload newly created responsibility.");
    }

    public async Task<ResponsibilityResponse?> UpdateAsync(
        Guid responsibilityId,
        SaveResponsibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var resp = await _dbContext.Responsibilities.SingleOrDefaultAsync(r => r.ResponsibilityId == responsibilityId, cancellationToken);
        if (resp is null)
        {
            return null;
        }

        var name = ValidateName(request.Name);

        var duplicate = await _dbContext.Responsibilities.AnyAsync(
            r => r.TenantId == resp.TenantId && r.IsActive
                 && r.ResponsibilityId != responsibilityId && r.Name.ToLower() == name.ToLower(),
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"A responsibility named \"{name}\" already exists.");
        }

        if (request.JobRoleIds is not null)
        {
            var jobRoleIds = await ValidateJobRoleIdsAsync(resp.TenantId, request.JobRoleIds, cancellationToken);

            var currentLinks = await _dbContext.JobRoleResponsibilities
                .Where(jrr => jrr.ResponsibilityId == responsibilityId)
                .ToListAsync(cancellationToken);

            var currentRoleIds = currentLinks.Select(l => l.JobRoleId).ToHashSet();
            var requestedRoleIds = jobRoleIds.ToHashSet();

            foreach (var link in currentLinks.Where(l => !requestedRoleIds.Contains(l.JobRoleId)))
            {
                _dbContext.JobRoleResponsibilities.Remove(link);
            }

            var now = DateTime.UtcNow;
            foreach (var jobRoleId in requestedRoleIds.Where(id => !currentRoleIds.Contains(id)))
            {
                _dbContext.JobRoleResponsibilities.Add(new JobRoleResponsibility
                {
                    JobRoleId = jobRoleId,
                    ResponsibilityId = responsibilityId,
                    CreatedDate = now,
                });
            }
        }

        resp.Name = name;
        resp.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        resp.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(responsibilityId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid responsibilityId, CancellationToken cancellationToken = default)
    {
        var resp = await _dbContext.Responsibilities.SingleOrDefaultAsync(r => r.ResponsibilityId == responsibilityId, cancellationToken);
        if (resp is null)
        {
            return false;
        }

        _dbContext.Responsibilities.Remove(resp);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Finds-or-creates Responsibility rows for every item listed in
    /// JobRole.ResponsibilitiesJson, and updates JobRoleResponsibility links.</summary>
    public async Task ReconcileForJobRoleAsync(JobRole jobRole, CancellationToken cancellationToken = default)
    {
        var names = System.Text.Json.JsonSerializer.Deserialize<List<string>>(jobRole.ResponsibilitiesJson) ?? [];
        var trimmedNames = names.Select(n => n.Trim()).Where(n => n.Length > 0).ToList();

        var existingLinks = await _dbContext.JobRoleResponsibilities
            .Where(jrr => jrr.JobRoleId == jobRole.JobRoleId)
            .Include(jrr => jrr.Responsibility)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var link in existingLinks)
        {
            if (!trimmedNames.Any(n => string.Equals(n, link.Responsibility.Name, StringComparison.OrdinalIgnoreCase)))
            {
                _dbContext.JobRoleResponsibilities.Remove(link);
            }
        }

        foreach (var name in trimmedNames)
        {
            if (existingLinks.Any(l => string.Equals(l.Responsibility.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var resp = await _dbContext.Responsibilities.FirstOrDefaultAsync(
                r => r.TenantId == jobRole.TenantId && r.Name.ToLower() == name.ToLower(),
                cancellationToken);

            if (resp is null)
            {
                resp = new Responsibility
                {
                    ResponsibilityId = Guid.NewGuid(),
                    TenantId = jobRole.TenantId,
                    Name = name,
                    IsActive = true,
                    CreatedDate = now,
                    UpdatedDate = now,
                };
                _dbContext.Responsibilities.Add(resp);
            }

            _dbContext.JobRoleResponsibilities.Add(new JobRoleResponsibility
            {
                JobRoleId = jobRole.JobRoleId,
                ResponsibilityId = resp.ResponsibilityId,
                CreatedDate = now,
            });
        }
    }

    private static string ValidateName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("Responsibility name is required.");
        }

        var trimmed = raw.Trim();
        if (trimmed.Length > 300)
        {
            throw new InvalidOperationException("Responsibility name cannot exceed 300 characters.");
        }

        return trimmed;
    }

    private async Task<List<Guid>> ValidateJobRoleIdsAsync(
        Guid tenantId,
        List<Guid>? jobRoleIds,
        CancellationToken cancellationToken)
    {
        if (jobRoleIds is null || jobRoleIds.Count == 0)
        {
            return [];
        }

        var distinct = jobRoleIds.Distinct().ToList();
        var validCount = await _dbContext.JobRoles
            .CountAsync(r => r.TenantId == tenantId && r.IsActive && distinct.Contains(r.JobRoleId), cancellationToken);

        if (validCount != distinct.Count)
        {
            throw new InvalidOperationException("One or more referenced Job Roles do not exist in this tenant.");
        }

        return distinct;
    }

    public static async Task<List<string>> GetCatalogNamesByRoleAsync(
        HumanOsDbContext dbContext,
        Guid jobRoleId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.JobRoleResponsibilities
            .AsNoTracking()
            .Where(jrr => jrr.JobRoleId == jobRoleId && jrr.Responsibility.IsActive)
            .Select(jrr => jrr.Responsibility.Name)
            .ToListAsync(cancellationToken);
    }

    public static async Task<Dictionary<Guid, List<string>>> GetCatalogNamesByRolesAsync(
        HumanOsDbContext dbContext,
        IEnumerable<Guid> jobRoleIds,
        CancellationToken cancellationToken = default)
    {
        var ids = jobRoleIds.ToList();
        if (ids.Count == 0) return [];

        var rows = await dbContext.JobRoleResponsibilities
            .AsNoTracking()
            .Where(jrr => ids.Contains(jrr.JobRoleId) && jrr.Responsibility.IsActive)
            .Select(jrr => new { jrr.JobRoleId, jrr.Responsibility.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.JobRoleId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Name).ToList());
    }

    public static List<string> UnionWithCatalog(List<string> jsonList, List<string>? catalogNames)
    {
        if (catalogNames is null || catalogNames.Count == 0) return jsonList;

        var result = new List<string>(jsonList);
        var seen = new HashSet<string>(jsonList.Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
        foreach (var name in catalogNames)
        {
            var trimmed = name.Trim();
            if (trimmed.Length > 0 && seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }
        return result;
    }

    private static ResponsibilityResponse ToResponse(Responsibility resp, IReadOnlyList<ResponsibilityJobRoleLinkResponse> links) =>
        new()
        {
            ResponsibilityId = resp.ResponsibilityId,
            TenantId = resp.TenantId,
            Name = resp.Name,
            Description = resp.Description,
            IsActive = resp.IsActive,
            JobRoles = links,
            CreatedDate = resp.CreatedDate,
            UpdatedDate = resp.UpdatedDate,
        };
}
