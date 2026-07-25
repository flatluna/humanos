using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// "Inscribirme" (2026-07-24) — POST /people/{personId}/capabilities/{capabilityId}/enroll.
/// Creates an explicit CapabilityEnrollment record, distinct from just
/// running the course via GET /capabilities/{id} (which stays free/open to
/// anyone). See CapabilityEnrollmentService.EnrollAsync.
/// </summary>
public sealed class EnrollCapabilityFunction
{
    private readonly CapabilityEnrollmentService _capabilityEnrollmentService;

    public EnrollCapabilityFunction(CapabilityEnrollmentService capabilityEnrollmentService)
    {
        _capabilityEnrollmentService = capabilityEnrollmentService;
    }

    [Function("EnrollCapability")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/capabilities/{capabilityId:guid}/enroll")]
        HttpRequestData request,
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _capabilityEnrollmentService.EnrollAsync(personId, capabilityId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "CapabilityNotFound", ex.Message, cancellationToken);
        }
    }
}
