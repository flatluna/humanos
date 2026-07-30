using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// "Quitar" (2026-07-25) — DELETE /people/{personId}/programs/{programId}/enroll.
/// Removes the explicit ProgramEnrollment record created via
/// EnrollProgramFunction. See ProgramEnrollmentService.UnenrollAsync.
/// </summary>
public sealed class UnenrollProgramFunction
{
    private readonly ProgramEnrollmentService _programEnrollmentService;

    public UnenrollProgramFunction(ProgramEnrollmentService programEnrollmentService)
    {
        _programEnrollmentService = programEnrollmentService;
    }

    [Function("UnenrollProgram")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "people/{personId:guid}/programs/{programId:guid}/enroll")]
        HttpRequestData request,
        Guid personId,
        Guid programId,
        CancellationToken cancellationToken)
    {
        var result = await _programEnrollmentService.UnenrollAsync(personId, programId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
    }
}
