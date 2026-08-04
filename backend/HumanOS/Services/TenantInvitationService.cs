using HumanOS.Contracts.People;
using HumanOS.Data;
using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class TenantInvitationService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly EntraInvitationProvisioningService _entraInvitationProvisioningService;

    public TenantInvitationService(
        HumanOsDbContext dbContext,
        EntraInvitationProvisioningService entraInvitationProvisioningService)
    {
        _dbContext = dbContext;
        _entraInvitationProvisioningService = entraInvitationProvisioningService;
    }

    public async Task<IReadOnlyList<TenantInvitationResponse>> GetByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<TenantInvitation>()
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedDate)
            .Select(ToResponseExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantInvitationResponse> CreateAsync(
        Guid tenantId,
        CreateTenantInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();
        var normalizedEmail = email.ToLowerInvariant();

        if (!await _dbContext.Tenants.AnyAsync(t => t.TenantId == tenantId, cancellationToken))
        {
            throw new InvalidOperationException($"Tenant {tenantId} was not found.");
        }

        if (request.DepartmentId is Guid departmentId)
        {
            var belongsToTenant = await _dbContext.Departments
                .AnyAsync(d => d.DepartmentId == departmentId && d.TenantId == tenantId, cancellationToken);
            if (!belongsToTenant)
            {
                throw new InvalidOperationException("Department does not belong to this tenant.");
            }
        }

        if (request.JobRoleId is Guid jobRoleId)
        {
            var belongsToTenant = await _dbContext.JobRoles
                .AnyAsync(r => r.JobRoleId == jobRoleId && r.TenantId == tenantId, cancellationToken);
            if (!belongsToTenant)
            {
                throw new InvalidOperationException("Job role does not belong to this tenant.");
            }
        }

        if (request.ManagerId is Guid managerId)
        {
            var belongsToTenant = await _dbContext.People
                .AnyAsync(p => p.PersonId == managerId && p.TenantId == tenantId, cancellationToken);
            if (!belongsToTenant)
            {
                throw new InvalidOperationException("Manager does not belong to this tenant.");
            }
        }

        var alreadyPending = await _dbContext.Set<TenantInvitation>()
            .AnyAsync(
                i => i.TenantId == tenantId
                     && i.NormalizedEmail == normalizedEmail
                     && i.Status == TenantInvitationStatus.Pending,
                cancellationToken);

        if (alreadyPending)
        {
            throw new InvalidOperationException("There is already a pending invitation for this email.");
        }

        var orgRole = ParseRoleOrDefault(request.OrgRole);
        var now = DateTime.UtcNow;

        var invitation = new TenantInvitation
        {
            TenantInvitationId = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim(),
            DepartmentId = request.DepartmentId,
            JobRoleId = request.JobRoleId,
            ManagerId = request.ManagerId,
            OrgRole = orgRole,
            Status = TenantInvitationStatus.Pending,
            CreatedDate = now,
            UpdatedDate = now,
        };

        _dbContext.Set<TenantInvitation>().Add(invitation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _entraInvitationProvisioningService.InviteMemberAsync(
                email,
                BuildDisplayName(request.FirstName, request.LastName),
                cancellationToken);
        }
        catch
        {
            // Keep HumanOS and Entra in sync: if Entra provisioning fails,
            // remove the local pending record created in this request.
            _dbContext.Set<TenantInvitation>().Remove(invitation);
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        return ToResponse(invitation);
    }

    public async Task DeletePendingAsync(
        Guid tenantId,
        Guid tenantInvitationId,
        CancellationToken cancellationToken = default)
    {
        var invitation = await _dbContext.Set<TenantInvitation>()
            .SingleOrDefaultAsync(
                i => i.TenantInvitationId == tenantInvitationId && i.TenantId == tenantId,
                cancellationToken);

        if (invitation is null)
        {
            throw new KeyNotFoundException("Invitation was not found for this tenant.");
        }

        if (invitation.Status != TenantInvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only pending invitations can be deleted.");
        }

        _dbContext.Set<TenantInvitation>().Remove(invitation);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// If a signed-in Azure identity matches a pending invitation by email,
    /// this claims that invitation into a real Person.Tenant membership.
    ///
    /// Returns true when a pending invitation was consumed and the person
    /// was created/linked to the tenant; false when no eligible invitation
    /// exists or when the identity is already linked to a tenant.
    /// </summary>
    public async Task<bool> TryClaimPendingInvitationAsync(
        string azureOid,
        string azureTid,
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedOid = azureOid.Trim();
        var normalizedTid = azureTid.Trim();
        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return false;
        }

        var invitation = await _dbContext.TenantInvitations
            .Where(i => i.NormalizedEmail == normalizedEmail && i.Status == TenantInvitationStatus.Pending)
            .Join(
                _dbContext.Tenants,
                i => i.TenantId,
                t => t.TenantId,
                (i, t) => new { Invitation = i, Tenant = t })
            .Where(x => x.Tenant.AzureTenantId == null || x.Tenant.AzureTenantId == normalizedTid)
            .OrderByDescending(x => x.Invitation.CreatedDate)
            .Select(x => x.Invitation)
            .FirstOrDefaultAsync(cancellationToken);

        if (invitation is null)
        {
            return false;
        }

        var person = await _dbContext.People
            .SingleOrDefaultAsync(
                p => p.AzureOid == normalizedOid && p.AzureTid == normalizedTid,
                cancellationToken);

        // Already linked to a company account; don't consume invitation.
        if (person is not null && person.TenantId is not null)
        {
            return false;
        }

        var now = DateTime.UtcNow;

        if (person is null)
        {
            person = new Person
            {
                PersonId = Guid.NewGuid(),
                TenantId = invitation.TenantId,
                AzureOid = normalizedOid,
                AzureTid = normalizedTid,
                Email = invitation.Email,
                IsActive = true,
                LastLoginDate = now,
                DepartmentId = invitation.DepartmentId,
                JobRoleId = invitation.JobRoleId,
                ManagerId = invitation.ManagerId,
                OrgRole = invitation.OrgRole,
                CreatedDate = now,
                UpdatedDate = now,
            };

            _dbContext.People.Add(person);
        }
        else
        {
            person.TenantId = invitation.TenantId;
            person.Email = string.IsNullOrWhiteSpace(person.Email) ? invitation.Email : person.Email;
            person.DepartmentId = invitation.DepartmentId;
            person.JobRoleId = invitation.JobRoleId;
            person.ManagerId = invitation.ManagerId;
            person.OrgRole = invitation.OrgRole;
            person.IsActive = true;
            person.LastLoginDate = now;
            person.UpdatedDate = now;
        }

        invitation.Status = TenantInvitationStatus.Accepted;
        invitation.AcceptedDate = now;
        invitation.UpdatedDate = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static PersonOrgRole ParseRoleOrDefault(string? orgRole)
    {
        if (string.IsNullOrWhiteSpace(orgRole))
        {
            return PersonOrgRole.Employee;
        }

        if (!Enum.TryParse<PersonOrgRole>(orgRole, ignoreCase: true, out var parsed))
        {
            throw new InvalidOperationException("OrgRole must be Employee, Manager, or HRAdmin.");
        }

        return parsed;
    }

    private static string? BuildDisplayName(string? firstName, string? lastName)
    {
        var fullName = $"{firstName?.Trim()} {lastName?.Trim()}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? null : fullName;
    }

    private static TenantInvitationResponse ToResponse(TenantInvitation invitation) => new()
    {
        TenantInvitationId = invitation.TenantInvitationId,
        TenantId = invitation.TenantId,
        Email = invitation.Email,
        FirstName = invitation.FirstName,
        LastName = invitation.LastName,
        DepartmentId = invitation.DepartmentId,
        JobRoleId = invitation.JobRoleId,
        ManagerId = invitation.ManagerId,
        OrgRole = invitation.OrgRole.ToString(),
        Status = invitation.Status.ToString(),
        CreatedDate = invitation.CreatedDate,
        UpdatedDate = invitation.UpdatedDate,
        AcceptedDate = invitation.AcceptedDate,
        RevokedDate = invitation.RevokedDate,
    };

    private static System.Linq.Expressions.Expression<Func<TenantInvitation, TenantInvitationResponse>> ToResponseExpression()
    {
        return invitation => new TenantInvitationResponse
        {
            TenantInvitationId = invitation.TenantInvitationId,
            TenantId = invitation.TenantId,
            Email = invitation.Email,
            FirstName = invitation.FirstName,
            LastName = invitation.LastName,
            DepartmentId = invitation.DepartmentId,
            JobRoleId = invitation.JobRoleId,
            ManagerId = invitation.ManagerId,
            OrgRole = invitation.OrgRole.ToString(),
            Status = invitation.Status.ToString(),
            CreatedDate = invitation.CreatedDate,
            UpdatedDate = invitation.UpdatedDate,
            AcceptedDate = invitation.AcceptedDate,
            RevokedDate = invitation.RevokedDate,
        };
    }
}
