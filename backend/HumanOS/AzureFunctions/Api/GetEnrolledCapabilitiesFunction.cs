using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// "Mis Capacidades" (2026-07-24) — GET /people/{personId}/enrolled-capabilities.
/// Returns every standalone Capability (no Program) the person has
/// explicitly enrolled in via "Inscribirme", each with real progress
/// derived from the Capability Graph. Sibling endpoint to
/// GetEnrolledProgramsFunction ("Mis Programas"). See
/// CapabilityEnrollmentService.GetEnrolledCapabilitiesAsync.
/// </summary>
public sealed class GetEnrolledCapabilitiesFunction
{
    private readonly CapabilityEnrollmentService _capabilityEnrollmentService;

    public GetEnrolledCapabilitiesFunction(CapabilityEnrollmentService capabilityEnrollmentService)
    {
        _capabilityEnrollmentService = capabilityEnrollmentService;
    }

    [Function("GetEnrolledCapabilities")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/enrolled-capabilities")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var enrolledCapabilities = await _capabilityEnrollmentService.GetEnrolledCapabilitiesAsync(personId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, enrolledCapabilities, cancellationToken: cancellationToken);
    }
}
