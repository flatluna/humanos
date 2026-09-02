using HumanOS.Contracts.OrgStructure;
using HumanOS.Data;
using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>CRUD for a Tenant's Certification catalog — reusable
/// professional certifications shareable across one or many Job Roles.</summary>
public sealed class CertificationService
{
    private readonly HumanOsDbContext _dbContext;

    public CertificationService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CertificationResponse>> GetByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var certifications = await _dbContext.Certifications
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var certificationIds = certifications.Select(c => c.CertificationId).ToList();

        var links = await _dbContext.JobRoleCertifications
            .AsNoTracking()
            .Where(jrc => certificationIds.Contains(jrc.CertificationId))
            .Select(jrc => new
            {
                jrc.CertificationId,
                jrc.JobRoleId,
                JobRoleTitle = jrc.JobRole.Title,
                DepartmentName = jrc.JobRole.Department != null ? jrc.JobRole.Department.Name : "General",
            })
            .ToListAsync(cancellationToken);

        var linksByCert = links
            .GroupBy(l => l.CertificationId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<CertificationJobRoleLinkResponse>)g
                    .Select(l => new CertificationJobRoleLinkResponse
                    {
                        JobRoleId = l.JobRoleId,
                        JobRoleTitle = l.JobRoleTitle,
                        DepartmentName = l.DepartmentName,
                    })
                    .OrderBy(l => l.JobRoleTitle)
                    .ToList());

        return certifications
            .Select(c => ToResponse(c, linksByCert.GetValueOrDefault(c.CertificationId, [])))
            .ToList();
    }

    public async Task<CertificationResponse?> GetByIdAsync(
        Guid certificationId,
        CancellationToken cancellationToken = default)
    {
        var cert = await _dbContext.Certifications
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.CertificationId == certificationId && c.IsActive, cancellationToken);

        if (cert is null)
        {
            return null;
        }

        var links = await _dbContext.JobRoleCertifications
            .AsNoTracking()
            .Where(jrc => jrc.CertificationId == certificationId)
            .Select(jrc => new CertificationJobRoleLinkResponse
            {
                JobRoleId = jrc.JobRoleId,
                JobRoleTitle = jrc.JobRole.Title,
                DepartmentName = jrc.JobRole.Department != null ? jrc.JobRole.Department.Name : "General",
            })
            .OrderBy(l => l.JobRoleTitle)
            .ToListAsync(cancellationToken);

        return ToResponse(cert, links);
    }

    public async Task<CertificationResponse> CreateAsync(
        Guid tenantId,
        SaveCertificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = ValidateName(request.Name);

        var duplicate = await _dbContext.Certifications.AnyAsync(
            c => c.TenantId == tenantId && c.IsActive && c.Name.ToLower() == name.ToLower(),
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"A certification named \"{name}\" already exists.");
        }

        var now = DateTime.UtcNow;
        var cert = new Certification
        {
            CertificationId = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Issuer = string.IsNullOrWhiteSpace(request.Issuer) ? null : request.Issuer.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
            CreatedDate = now,
            UpdatedDate = now,
        };
        _dbContext.Certifications.Add(cert);

        if (request.JobRoleIds is not null && request.JobRoleIds.Count > 0)
        {
            var jobRoleIds = await ValidateJobRoleIdsAsync(tenantId, request.JobRoleIds, cancellationToken);
            foreach (var jobRoleId in jobRoleIds)
            {
                _dbContext.JobRoleCertifications.Add(new JobRoleCertification
                {
                    JobRoleId = jobRoleId,
                    CertificationId = cert.CertificationId,
                    CreatedDate = now,
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(cert.CertificationId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload newly created certification.");
    }

    public async Task<CertificationResponse?> UpdateAsync(
        Guid certificationId,
        SaveCertificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var cert = await _dbContext.Certifications.SingleOrDefaultAsync(c => c.CertificationId == certificationId, cancellationToken);
        if (cert is null)
        {
            return null;
        }

        var name = ValidateName(request.Name);

        var duplicate = await _dbContext.Certifications.AnyAsync(
            c => c.TenantId == cert.TenantId && c.IsActive
                 && c.CertificationId != certificationId && c.Name.ToLower() == name.ToLower(),
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"A certification named \"{name}\" already exists.");
        }

        if (request.JobRoleIds is not null)
        {
            var jobRoleIds = await ValidateJobRoleIdsAsync(cert.TenantId, request.JobRoleIds, cancellationToken);

            var currentLinks = await _dbContext.JobRoleCertifications
                .Where(jrc => jrc.CertificationId == certificationId)
                .ToListAsync(cancellationToken);

            var currentRoleIds = currentLinks.Select(l => l.JobRoleId).ToHashSet();
            var requestedRoleIds = jobRoleIds.ToHashSet();

            foreach (var link in currentLinks.Where(l => !requestedRoleIds.Contains(l.JobRoleId)))
            {
                _dbContext.JobRoleCertifications.Remove(link);
            }

            var now = DateTime.UtcNow;
            foreach (var jobRoleId in requestedRoleIds.Where(id => !currentRoleIds.Contains(id)))
            {
                _dbContext.JobRoleCertifications.Add(new JobRoleCertification
                {
                    JobRoleId = jobRoleId,
                    CertificationId = certificationId,
                    CreatedDate = now,
                });
            }
        }

        cert.Name = name;
        cert.Issuer = string.IsNullOrWhiteSpace(request.Issuer) ? null : request.Issuer.Trim();
        cert.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        cert.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(certificationId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid certificationId, CancellationToken cancellationToken = default)
    {
        var cert = await _dbContext.Certifications.SingleOrDefaultAsync(c => c.CertificationId == certificationId, cancellationToken);
        if (cert is null)
        {
            return false;
        }

        _dbContext.Certifications.Remove(cert);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Finds-or-creates Certification rows for every item listed in
    /// JobRole.RequiredCertificationsJson, and updates JobRoleCertification links.</summary>
    public async Task ReconcileForJobRoleAsync(JobRole jobRole, CancellationToken cancellationToken = default)
    {
        var names = System.Text.Json.JsonSerializer.Deserialize<List<string>>(jobRole.RequiredCertificationsJson) ?? [];
        var trimmedNames = names.Select(n => n.Trim()).Where(n => n.Length > 0).ToList();

        var existingLinks = await _dbContext.JobRoleCertifications
            .Where(jrc => jrc.JobRoleId == jobRole.JobRoleId)
            .Include(jrc => jrc.Certification)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var link in existingLinks)
        {
            if (!trimmedNames.Any(n => string.Equals(n, link.Certification.Name, StringComparison.OrdinalIgnoreCase)))
            {
                _dbContext.JobRoleCertifications.Remove(link);
            }
        }

        foreach (var name in trimmedNames)
        {
            if (existingLinks.Any(l => string.Equals(l.Certification.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var cert = await _dbContext.Certifications.FirstOrDefaultAsync(
                c => c.TenantId == jobRole.TenantId && c.Name.ToLower() == name.ToLower(),
                cancellationToken);

            if (cert is null)
            {
                cert = new Certification
                {
                    CertificationId = Guid.NewGuid(),
                    TenantId = jobRole.TenantId,
                    Name = name,
                    IsActive = true,
                    CreatedDate = now,
                    UpdatedDate = now,
                };
                _dbContext.Certifications.Add(cert);
            }

            _dbContext.JobRoleCertifications.Add(new JobRoleCertification
            {
                JobRoleId = jobRole.JobRoleId,
                CertificationId = cert.CertificationId,
                CreatedDate = now,
            });
        }
    }

    private static string ValidateName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("Certification name is required.");
        }

        var trimmed = raw.Trim();
        if (trimmed.Length > 300)
        {
            throw new InvalidOperationException("Certification name cannot exceed 300 characters.");
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
        return await dbContext.JobRoleCertifications
            .AsNoTracking()
            .Where(jrc => jrc.JobRoleId == jobRoleId && jrc.Certification.IsActive)
            .Select(jrc => jrc.Certification.Name)
            .ToListAsync(cancellationToken);
    }

    public static async Task<Dictionary<Guid, List<string>>> GetCatalogNamesByRolesAsync(
        HumanOsDbContext dbContext,
        IEnumerable<Guid> jobRoleIds,
        CancellationToken cancellationToken = default)
    {
        var ids = jobRoleIds.ToList();
        if (ids.Count == 0) return [];

        var rows = await dbContext.JobRoleCertifications
            .AsNoTracking()
            .Where(jrc => ids.Contains(jrc.JobRoleId) && jrc.Certification.IsActive)
            .Select(jrc => new { jrc.JobRoleId, jrc.Certification.Name })
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

    private static CertificationResponse ToResponse(Certification cert, IReadOnlyList<CertificationJobRoleLinkResponse> links) =>
        new()
        {
            CertificationId = cert.CertificationId,
            TenantId = cert.TenantId,
            Name = cert.Name,
            Issuer = cert.Issuer,
            Description = cert.Description,
            IsActive = cert.IsActive,
            JobRoles = links,
            CreatedDate = cert.CreatedDate,
            UpdatedDate = cert.UpdatedDate,
        };
}
