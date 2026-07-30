using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// "Inscribirme" (2026-07-25) — POST /people/{personId}/programs/{programId}/enroll.
/// Creates an explicit ProgramEnrollment record from the Programs catalog,
/// distinct from accepting a recommended Program during the Growth Plan's
/// Starting Point step. See ProgramEnrollmentService.EnrollAsync.
/// </summary>
public sealed class EnrollProgramFunction
{
    private readonly ProgramEnrollmentService _programEnrollmentService;

    public EnrollProgramFunction(ProgramEnrollmentService programEnrollmentService)
    {
        _programEnrollmentService = programEnrollmentService;
    }

    [Function("EnrollProgram")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/programs/{programId:guid}/enroll")]
        HttpRequestData request,
        Guid personId,
        Guid programId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _programEnrollmentService.EnrollAsync(personId, programId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ProgramNotFound", ex.Message, cancellationToken);
        }
    }
}
