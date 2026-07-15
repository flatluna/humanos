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

    public GetMeFunction(PersonService personService, TenantService tenantService)
    {
        _personService = personService;
        _tenantService = tenantService;
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

        var person = await _personService.GetByAzureIdentityAsync(azureOid, azureTid, cancellationToken);

        if (person is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonNotFound", "No Human OS account exists yet for this identity.", cancellationToken);
        }

        var tenant = await _tenantService.GetByIdAsync(person.TenantId, cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new MeResponse
        {
            PersonId = person.PersonId,
            TenantId = person.TenantId,
            TenantName = tenant?.Name ?? string.Empty,
            Email = person.Email,
        });

        return response;
    }
}
