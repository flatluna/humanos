using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetCapabilitiesFunction
{
    private readonly CapabilityService _capabilityService;
    private readonly PersonService _personService;

    public GetCapabilitiesFunction(CapabilityService capabilityService, PersonService personService)
    {
        _capabilityService = capabilityService;
        _personService = personService;
    }

    [Function("GetCapabilities")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "capabilities")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var language = query["language"] ?? "en";
        var domain = query["domain"];
        var subject = query["subject"];
        var contentLanguage = query["contentLanguage"];

        // Ownership/visibility filter (2026-08-05): identity headers are
        // OPTIONAL here — this catalog is browsed by callers that may not
        // be authenticated yet (e.g. Capability Studio has no real auth
        // wired in yet). Missing/unresolvable identity simply means "only
        // the public/official catalog", never an error, same as before
        // this feature existed.
        Guid? callerPersonId = null;
        Guid? callerTenantId = null;

        // Studio sessions launched from a parent app that has no HumanOS
        // Person record (e.g. an NGO admin) carry their tenant directly via
        // this header instead — see capabilitystudio's getIdentityHeaders().
        if (request.Headers.TryGetValues("X-Studio-Tenant-Id", out var studioTenantValues) &&
            Guid.TryParse(studioTenantValues.FirstOrDefault(), out var studioTenantId))
        {
            callerTenantId = studioTenantId;
        }
        else if (request.Headers.TryGetValues("X-Azure-OID", out var oidValues) && !string.IsNullOrWhiteSpace(oidValues.FirstOrDefault()) &&
            request.Headers.TryGetValues("X-Azure-TID", out var tidValues) && !string.IsNullOrWhiteSpace(tidValues.FirstOrDefault()))
        {
            var callerPerson = await _personService.GetByAzureIdentityAsync(oidValues.First(), tidValues.First(), cancellationToken);
            if (callerPerson is not null)
            {
                callerPersonId = callerPerson.PersonId;
                callerTenantId = callerPerson.TenantId;
            }
        }

        var capabilities = await _capabilityService.GetActiveAsync(
            language, domain, subject, contentLanguage, cancellationToken, callerPersonId, callerTenantId);

        return await FunctionResponseFactory.SuccessResponseAsync(request, capabilities, cancellationToken: cancellationToken);
    }
}
