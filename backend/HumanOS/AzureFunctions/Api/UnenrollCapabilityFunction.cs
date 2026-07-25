using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// "Quitar" inscripción (2026-07-24) — DELETE
/// /people/{personId}/capabilities/{capabilityId}/enroll. Only removes the
/// CapabilityEnrollment row; never touches LearningSession/progress data,
/// so re-enrolling later resumes exactly where progress was left. See
/// CapabilityEnrollmentService.UnenrollAsync.
/// </summary>
public sealed class UnenrollCapabilityFunction
{
    private readonly CapabilityEnrollmentService _capabilityEnrollmentService;

    public UnenrollCapabilityFunction(CapabilityEnrollmentService capabilityEnrollmentService)
    {
        _capabilityEnrollmentService = capabilityEnrollmentService;
    }

    [Function("UnenrollCapability")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "people/{personId:guid}/capabilities/{capabilityId:guid}/enroll")]
        HttpRequestData request,
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var result = await _capabilityEnrollmentService.UnenrollAsync(personId, capabilityId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
    }
}
