using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// "Mis Programas" (2026-07-24) — GET /people/{personId}/enrolled-programs.
/// Returns every Program the person is enrolled in (Growth Plan Step 3's
/// accepted recommendations that resolved to a real Program), each with
/// its ordered Capabilities and this person's progress on them, scoped
/// per-Program. See GrowthPlanService.GetEnrolledProgramsAsync.
/// </summary>
public sealed class GetEnrolledProgramsFunction
{
    private readonly GrowthPlanService _growthPlanService;

    public GetEnrolledProgramsFunction(GrowthPlanService growthPlanService)
    {
        _growthPlanService = growthPlanService;
    }

    [Function("GetEnrolledPrograms")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId}/enrolled-programs")]
        HttpRequestData request,
        string personId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(personId, out var parsedPersonId))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidPersonId", "The route parameter personId must be a valid GUID.", cancellationToken);
        }

        var enrolledPrograms = await _growthPlanService.GetEnrolledProgramsAsync(parsedPersonId, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, enrolledPrograms, cancellationToken: cancellationToken);
    }
}
