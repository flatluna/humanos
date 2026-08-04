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

        return ToResponse(person);
    }

    private static PersonResponse ToResponse(Person person)
    {
        return new PersonResponse
        {
            PersonId = person.PersonId,
            TenantId = person.TenantId,
            Email = person.Email,
            IsActive = person.IsActive,
            LastLoginDate = person.LastLoginDate,
            CreatedDate = person.CreatedDate,
            UpdatedDate = person.UpdatedDate,
            OrgRole = person.OrgRole.ToString(),
            DepartmentId = person.DepartmentId,
            JobRoleId = person.JobRoleId,
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

        return ToResponse(person);
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

        return ToResponse(person);
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

        return ToResponse(person);
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
            return ToResponse(person);
        }

        person.IsActive = false;
        person.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(person);
    }

    /// <summary>Full people directory for a Tenant (org-structure admin
    /// console pickers/list) — includes Department/JobRole/Manager display
    /// names via a left join against PersonProfile for DisplayName.</summary>
    public async Task<IReadOnlyList<TenantPersonResponse>> GetTenantDirectoryAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var people = await _dbContext.People
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => new
            {
                Person = p,
                DepartmentName = p.Department != null ? p.Department.Name : null,
                JobRoleTitle = p.JobRole != null ? p.JobRole.Title : null,
                ManagerId = p.Manager != null ? p.Manager.PersonId : (Guid?)null,
            })
            .ToListAsync(cancellationToken);

        if (people.Count == 0)
        {
            return [];
        }

        var profiles = await _dbContext.PersonProfiles
            .AsNoTracking()
            .Where(pp => people.Select(x => x.Person.PersonId).Contains(pp.PersonId))
            .ToDictionaryAsync(pp => pp.PersonId, cancellationToken);

        string? DisplayNameFor(Guid personId, string? email) =>
            profiles.TryGetValue(personId, out var profile)
                ? (profile.DisplayName ?? $"{profile.FirstName} {profile.LastName}".Trim())
                : email;

        return people
            .Select(x => new TenantPersonResponse
            {
                PersonId = x.Person.PersonId,
                Email = x.Person.Email,
                DisplayName = DisplayNameFor(x.Person.PersonId, x.Person.Email),
                DepartmentId = x.Person.DepartmentId,
                DepartmentName = x.DepartmentName,
                JobRoleId = x.Person.JobRoleId,
                JobRoleTitle = x.JobRoleTitle,
                ManagerId = x.ManagerId,
                ManagerDisplayName = x.ManagerId is Guid managerId
                    ? DisplayNameFor(managerId, null)
                    : null,
                OrgRole = x.Person.OrgRole.ToString(),
                IsActive = x.Person.IsActive,
            })
            .OrderBy(x => x.DisplayName)
            .ToList();
    }

    /// <summary>Assigns/changes a person's Department, JobRole, Manager,
    /// and/or permission tier (HRAdmin-only action, enforced by the
    /// calling Function). Every field is a full replace — passing null for
    /// Department/JobRole/Manager clears that assignment. Validates that
    /// Department/JobRole/Manager (when provided) belong to the SAME
    /// tenant as the person; throws <see cref="InvalidOperationException"/>
    /// otherwise (mapped to 400 by the calling Function).</summary>
    public async Task<TenantPersonResponse?> UpdateOrgAssignmentAsync(
        Guid personId,
        Guid? departmentId,
        Guid? jobRoleId,
        Guid? managerId,
        PersonOrgRole? orgRole,
        CancellationToken cancellationToken = default)
    {
        var person = await _dbContext.People
            .SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);

        if (person is null)
        {
            return null;
        }

        if (departmentId is Guid deptId)
        {
            var departmentExists = await _dbContext.Departments
                .AnyAsync(d => d.DepartmentId == deptId && d.TenantId == person.TenantId, cancellationToken);
            if (!departmentExists)
            {
                throw new InvalidOperationException($"Department {deptId} does not belong to this person's tenant.");
            }
        }

        if (jobRoleId is Guid roleId)
        {
            var jobRoleExists = await _dbContext.JobRoles
                .AnyAsync(r => r.JobRoleId == roleId && r.TenantId == person.TenantId, cancellationToken);
            if (!jobRoleExists)
            {
                throw new InvalidOperationException($"JobRole {roleId} does not belong to this person's tenant.");
            }
        }

        if (managerId is Guid mgrId)
        {
            if (mgrId == personId)
            {
                throw new InvalidOperationException("A person cannot be their own manager.");
            }

            var managerExists = await _dbContext.People
                .AnyAsync(p => p.PersonId == mgrId && p.TenantId == person.TenantId, cancellationToken);
            if (!managerExists)
            {
                throw new InvalidOperationException($"Manager {mgrId} does not belong to this person's tenant.");
            }
        }

        person.DepartmentId = departmentId;
        person.JobRoleId = jobRoleId;
        person.ManagerId = managerId;
        if (orgRole is PersonOrgRole role)
        {
            person.OrgRole = role;
        }
        person.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var directory = await GetTenantDirectoryAsync(person.TenantId!.Value, cancellationToken);
        return directory.Single(x => x.PersonId == personId);
    }
}
