using HumanOS.Contracts.OrgStructure;
using HumanOS.Data;
using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>CRUD for a Tenant's Tools and Technologies catalog.</summary>
public sealed class ToolService
{
    private readonly HumanOsDbContext _dbContext;

    public ToolService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Tool>> GetByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tools
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Finds-or-creates Tool rows for every item listed in
    /// JobRole.ToolsJson, and updates JobRoleTool links.</summary>
    public async Task ReconcileForJobRoleAsync(JobRole jobRole, CancellationToken cancellationToken = default)
    {
        var names = System.Text.Json.JsonSerializer.Deserialize<List<string>>(jobRole.ToolsJson) ?? [];
        var trimmedNames = names.Select(n => n.Trim()).Where(n => n.Length > 0).ToList();

        var existingLinks = await _dbContext.JobRoleTools
            .Where(jrt => jrt.JobRoleId == jobRole.JobRoleId)
            .Include(jrt => jrt.Tool)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var link in existingLinks)
        {
            if (!trimmedNames.Any(n => string.Equals(n, link.Tool.Name, StringComparison.OrdinalIgnoreCase)))
            {
                _dbContext.JobRoleTools.Remove(link);
            }
        }

        foreach (var name in trimmedNames)
        {
            if (existingLinks.Any(l => string.Equals(l.Tool.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var tool = await _dbContext.Tools.FirstOrDefaultAsync(
                t => t.TenantId == jobRole.TenantId && t.Name.ToLower() == name.ToLower(),
                cancellationToken);

            if (tool is null)
            {
                tool = new Tool
                {
                    ToolId = Guid.NewGuid(),
                    TenantId = jobRole.TenantId,
                    Name = name,
                    IsActive = true,
                    CreatedDate = now,
                    UpdatedDate = now,
                };
                _dbContext.Tools.Add(tool);
            }

            _dbContext.JobRoleTools.Add(new JobRoleTool
            {
                JobRoleId = jobRole.JobRoleId,
                ToolId = tool.ToolId,
                CreatedDate = now,
            });
        }
    }

    public static async Task<List<string>> GetCatalogNamesByRoleAsync(
        HumanOsDbContext dbContext,
        Guid jobRoleId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.JobRoleTools
            .AsNoTracking()
            .Where(jrt => jrt.JobRoleId == jobRoleId && jrt.Tool.IsActive)
            .Select(jrt => jrt.Tool.Name)
            .ToListAsync(cancellationToken);
    }

    public static async Task<Dictionary<Guid, List<string>>> GetCatalogNamesByRolesAsync(
        HumanOsDbContext dbContext,
        IEnumerable<Guid> jobRoleIds,
        CancellationToken cancellationToken = default)
    {
        var ids = jobRoleIds.ToList();
        if (ids.Count == 0) return [];

        var rows = await dbContext.JobRoleTools
            .AsNoTracking()
            .Where(jrt => ids.Contains(jrt.JobRoleId) && jrt.Tool.IsActive)
            .Select(jrt => new { jrt.JobRoleId, jrt.Tool.Name })
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
}
