using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Capability Studio — lets a designer flip an EXISTING capability between
/// the public/official catalog (Global) and scoped-to-one-person/tenant
/// (Personal) after creation (2026-08-06 "cursos globales o personales"
/// feature). See <see cref="CapabilityService.UpdateVisibilityAsync"/>.
/// </summary>
public sealed class UpdateCapabilityVisibilityFunction
{
    private readonly CapabilityService _capabilityService;
    private readonly PersonService _personService;

    public UpdateCapabilityVisibilityFunction(CapabilityService capabilityService, PersonService personService)
    {
        _capabilityService = capabilityService;
        _personService = personService;
    }

    public sealed class UpdateRequest
    {
        public bool IsGlobal { get; set; }
    }

    [Function("UpdateCapabilityVisibility")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "patch",
            Route = "capabilities/{capabilityId:guid}/visibility")]
        HttpRequestData request,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var body = await JsonSerializer.DeserializeAsync<UpdateRequest>(
            request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken) ?? new UpdateRequest();

        // Same best-effort identity resolution as the capability creation
        // endpoints (see StartPdfCapabilityGraphFunction) — required here
        // only when marking Personal (the caller becomes the new owner).
        Guid? callerPersonId = null;
        Guid? callerTenantId = null;
        if (request.Headers.TryGetValues("X-Azure-OID", out var oidValues) && !string.IsNullOrWhiteSpace(oidValues.FirstOrDefault()) &&
            request.Headers.TryGetValues("X-Azure-TID", out var tidValues) && !string.IsNullOrWhiteSpace(tidValues.FirstOrDefault()))
        {
            var callerPerson = await _personService.GetByAzureIdentityAsync(oidValues.First(), tidValues.First(), cancellationToken);
            if (callerPerson is not null)
            {
                callerPersonId = callerPerson.PersonId;
                callerTenantId = callerPerson.TenantId;
            }
        }

        var result = await _capabilityService.UpdateVisibilityAsync(
            capabilityId, body.IsGlobal, callerPersonId, callerTenantId, cancellationToken);

        switch (result)
        {
            case CapabilityVisibilityUpdateResult.NotFound:
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "CapabilityNotFound",
                    "The requested capability was not found.", cancellationToken);
            case CapabilityVisibilityUpdateResult.CallerIdentityRequired:
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "CallerIdentityRequired",
                    "Marking a capability Personal requires a resolvable caller identity (X-Azure-OID/X-Azure-TID).", cancellationToken);
            case CapabilityVisibilityUpdateResult.Forbidden:
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.Forbidden, "NotCapabilityOwner",
                    "Only this capability's current owner may change its visibility.", cancellationToken);
            default:
                return request.CreateResponse(HttpStatusCode.NoContent);
        }
    }
}
