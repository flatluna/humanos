using System.Net;
using HumanOS.Contracts.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Looks up the real Human OS Tenant/Person for the currently signed-in
/// MSAL identity (see frontend src/auth/AuthContext.tsx), identified by
/// the <c>X-Azure-OID</c>/<c>X-Azure-TID</c> request headers.
///
/// TODO: This trusts the caller-supplied headers rather than validating
/// a bearer token server-side (matching the same pattern already used
/// by the reference genesis-personas app for its onboarding check).
/// Revisit if/when Human OS moves to real Bearer-token validation for
/// all endpoints.
/// </summary>
public sealed class GetMeFunction
{
    private readonly PersonService _personService;
    private readonly TenantService _tenantService;
    private readonly PersonProfileService _personProfileService;
    private readonly DepartmentService _departmentService;
    private readonly JobRoleService _jobRoleService;
    private readonly TenantInvitationService _tenantInvitationService;

    public GetMeFunction(
        PersonService personService,
        TenantService tenantService,
        PersonProfileService personProfileService,
        DepartmentService departmentService,
        JobRoleService jobRoleService,
        TenantInvitationService tenantInvitationService)
    {
        _personService = personService;
        _tenantService = tenantService;
        _personProfileService = personProfileService;
        _departmentService = departmentService;
        _jobRoleService = jobRoleService;
        _tenantInvitationService = tenantInvitationService;
    }

    [Function("GetMe")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValues("X-Azure-OID", out var oidValues) || string.IsNullOrWhiteSpace(oidValues.FirstOrDefault()))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingAzureOid", "The X-Azure-OID header is required.", cancellationToken);
        }

        if (!request.Headers.TryGetValues("X-Azure-TID", out var tidValues) || string.IsNullOrWhiteSpace(tidValues.FirstOrDefault()))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingAzureTid", "The X-Azure-TID header is required.", cancellationToken);
        }

        var azureOid = oidValues.First();
        var azureTid = tidValues.First();
        var email = request.Headers.TryGetValues("X-Azure-Email", out var emailValues)
            ? emailValues.FirstOrDefault()
            : null;

        var person = await _personService.GetByAzureIdentityAsync(azureOid, azureTid, cancellationToken);

        // Reconcile a pending invitation by email on first enterprise login,
        // so invited accounts can be auto-linked to their tenant.
        if (!string.IsNullOrWhiteSpace(email)
            && (person is null || person.TenantId is null))
        {
            await _tenantInvitationService.TryClaimPendingInvitationAsync(
                azureOid,
                azureTid,
                email,
                cancellationToken);

            person = await _personService.GetByAzureIdentityAsync(azureOid, azureTid, cancellationToken);
        }

        if (person is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonNotFound", "No Human OS account exists yet for this identity.", cancellationToken);
        }

        var tenant = person.TenantId is Guid tenantId
            ? await _tenantService.GetByIdAsync(tenantId, cancellationToken)
            : null;

        var profile = await _personProfileService.GetByPersonIdAsync(person.PersonId, cancellationToken);

        var department = person.DepartmentId is Guid departmentId
            ? await _departmentService.GetByIdAsync(departmentId, cancellationToken)
            : null;

        var jobRole = person.JobRoleId is Guid jobRoleId
            ? await _jobRoleService.GetResponseByIdAsync(jobRoleId, cancellationToken)
            : null;

        string? managerDisplayName = null;
        if (person.ManagerId is Guid managerId)
        {
            var managerProfile = await _personProfileService.GetByPersonIdAsync(managerId, cancellationToken);
            if (managerProfile != null && !string.IsNullOrWhiteSpace(managerProfile.DisplayName))
            {
                managerDisplayName = managerProfile.DisplayName;
            }
            else if (managerProfile != null && (!string.IsNullOrWhiteSpace(managerProfile.FirstName) || !string.IsNullOrWhiteSpace(managerProfile.LastName)))
            {
                managerDisplayName = $"{managerProfile.FirstName} {managerProfile.LastName}".Trim();
            }
            else
            {
                var managerPerson = await _personService.GetByIdAsync(managerId, cancellationToken);
                managerDisplayName = managerPerson?.Email;
            }
        }

        var response = await FunctionResponseFactory.SuccessResponseAsync(
            request,
            new MeResponse
            {
                PersonId = person.PersonId,
                TenantId = person.TenantId,
                TenantName = tenant?.Name,
                Email = person.Email,
                PreferredLanguage = profile?.PreferredLanguage,
                OrgRole = person.OrgRole.ToString(),
                DepartmentId = person.DepartmentId,
                DepartmentName = department?.Name,
                JobRoleId = person.JobRoleId,
                JobRoleTitle = jobRole?.Title,
                ManagerId = person.ManagerId,
                ManagerDisplayName = managerDisplayName,
            },
            cancellationToken: cancellationToken);
        response.Headers.Add("Cache-Control", "no-store");

        return response;
    }
}
