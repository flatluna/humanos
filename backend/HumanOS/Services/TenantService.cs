using HumanOS.Data;
using HumanOS.Models.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class TenantService
{
    private readonly HumanOsDbContext _dbContext;

    public TenantService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Tenant?> GetByIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                tenant => tenant.TenantId == tenantId,
                cancellationToken);
    }

    public async Task<Tenant?> GetBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();

        return await _dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                tenant => tenant.Slug == normalizedSlug,
                cancellationToken);
    }

    public async Task<Tenant?> GetByAzureTenantIdAsync(
        string azureTenantId,
        CancellationToken cancellationToken = default)
    {
        var normalizedAzureTenantId = azureTenantId.Trim();

        return await _dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                tenant =>
                    tenant.AzureTenantId == normalizedAzureTenantId,
                cancellationToken);
    }

    /// <summary>
    /// Creates a new Tenant (customer company) during onboarding. The slug
    /// is derived from the company name and de-duplicated with a numeric
    /// suffix if it already exists.
    /// </summary>
    public async Task<Tenant> CreateAsync(
        string name,
        string? domain,
        string? description,
        string? address,
        string? email,
        string? phone,
        string? azureTenantId,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = name.Trim();
        var slug = await GenerateUniqueSlugAsync(trimmedName, cancellationToken);

        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = trimmedName,
            Slug = slug,
            Domain = domain?.Trim(),
            Description = description?.Trim(),
            Address = address?.Trim(),
            Email = email?.Trim(),
            Phone = phone?.Trim(),
            CultureCode = "en-US",
            TimeZone = "UTC",
            AzureTenantId = azureTenantId?.Trim(),
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return tenant;
    }

    private async Task<string> GenerateUniqueSlugAsync(string name, CancellationToken cancellationToken)
    {
        var baseSlug = string.Join(
            '-',
            name.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "tenant";
        }

        var candidate = baseSlug;
        var suffix = 1;

        while (await _dbContext.Tenants.AnyAsync(tenant => tenant.Slug == candidate, cancellationToken))
        {
            suffix++;
            candidate = $"{baseSlug}-{suffix}";
        }

        return candidate;
    }

    public async Task<Tenant?> UpdateAsync(
        Guid tenantId,
        string name,
        string? domain,
        string? description,
        string cultureCode,
        string timeZone,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId,
                cancellationToken);

        if (tenant is null)
        {
            return null;
        }

        tenant.Name = name.Trim();

        tenant.Domain = string.IsNullOrWhiteSpace(domain)
            ? null
            : domain.Trim();

        tenant.Description = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        tenant.CultureCode = cultureCode.Trim();
        tenant.TimeZone = timeZone.Trim();
        tenant.IsActive = isActive;
        tenant.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return tenant;
    }
}
