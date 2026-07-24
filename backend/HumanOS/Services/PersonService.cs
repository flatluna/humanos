using HumanOS.Contracts.People;
using HumanOS.Data;
using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class PersonService
{
    private readonly HumanOsDbContext _dbContext;

    public PersonService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Creates the Person record for a real, already-authenticated MSAL
    /// identity during onboarding. <paramref name="azureOid"/> and
    /// <paramref name="azureTid"/> must come from the validated/trusted
    /// token claims — never invented — since they are how this person
    /// will be looked up on every subsequent sign-in
    /// (<see cref="GetByAzureIdentityAsync"/>). <paramref name="tenantId"/>
    /// is null for an individual (no-company) account — see
    /// CreateIndividualOnboardingFunction.
    /// </summary>
    public async Task<PersonResponse> CreateAsync(
        Guid? tenantId,
        string azureOid,
        string azureTid,
        string? email,
        CancellationToken cancellationToken = default)
    {
        var person = new Person
        {
            PersonId = Guid.NewGuid(),
            TenantId = tenantId,
            AzureOid = azureOid.Trim(),
            AzureTid = azureTid.Trim(),
            Email = email?.Trim(),
            IsActive = true,
            LastLoginDate = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
        };

        _dbContext.People.Add(person);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PersonResponse
        {
            PersonId = person.PersonId,
            TenantId = person.TenantId,
            Email = person.Email,
            IsActive = person.IsActive,
            LastLoginDate = person.LastLoginDate,
            CreatedDate = person.CreatedDate,
            UpdatedDate = person.UpdatedDate,
        };
    }

    public async Task<PersonResponse?> GetByIdAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var person = await _dbContext.People
            .AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.PersonId == personId,
                cancellationToken);

        if (person is null)
        {
            return null;
        }

        return new PersonResponse
        {
            PersonId = person.PersonId,
            TenantId = person.TenantId,
            Email = person.Email,
            IsActive = person.IsActive,
            LastLoginDate = person.LastLoginDate,
            CreatedDate = person.CreatedDate,
            UpdatedDate = person.UpdatedDate
        };
    }

    public async Task<PersonResponse?> GetByAzureIdentityAsync(
        string azureOid,
        string azureTid,
        CancellationToken cancellationToken = default)
    {
        var normalizedOid = azureOid.Trim();
        var normalizedTid = azureTid.Trim();

        var person = await _dbContext.People
            .AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.AzureOid == normalizedOid && p.AzureTid == normalizedTid,
                cancellationToken);

        if (person is null)
        {
            return null;
        }

        return new PersonResponse
        {
            PersonId = person.PersonId,
            TenantId = person.TenantId,
            Email = person.Email,
            IsActive = person.IsActive,
            LastLoginDate = person.LastLoginDate,
            CreatedDate = person.CreatedDate,
            UpdatedDate = person.UpdatedDate
        };
    }

    public async Task<PersonResponse?> UpdateLastLoginAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var person = await _dbContext.People
            .SingleOrDefaultAsync(
                p => p.PersonId == personId,
                cancellationToken);

        if (person is null)
        {
            return null;
        }

        person.LastLoginDate = DateTime.UtcNow;
        person.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PersonResponse
        {
            PersonId = person.PersonId,
            TenantId = person.TenantId,
            Email = person.Email,
            IsActive = person.IsActive,
            LastLoginDate = person.LastLoginDate,
            CreatedDate = person.CreatedDate,
            UpdatedDate = person.UpdatedDate
        };
    }

    public async Task<PersonResponse?> DeactivateAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var person = await _dbContext.People
            .SingleOrDefaultAsync(
                p => p.PersonId == personId,
                cancellationToken);

        if (person is null)
        {
            return null;
        }

        if (!person.IsActive)
        {
            return new PersonResponse
            {
                PersonId = person.PersonId,
                TenantId = person.TenantId,
                Email = person.Email,
                IsActive = person.IsActive,
                LastLoginDate = person.LastLoginDate,
                CreatedDate = person.CreatedDate,
                UpdatedDate = person.UpdatedDate
            };
        }

        person.IsActive = false;
        person.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PersonResponse
        {
            PersonId = person.PersonId,
            TenantId = person.TenantId,
            Email = person.Email,
            IsActive = person.IsActive,
            LastLoginDate = person.LastLoginDate,
            CreatedDate = person.CreatedDate,
            UpdatedDate = person.UpdatedDate
        };
    }
}
