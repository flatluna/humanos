using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// DELETE /departments/{departmentId:guid}/job-roles/{jobRoleId:guid} —
/// Detaches a Job Role from a Department without deleting the Job Role entity.
/// </summary>
public sealed class DetachDepartmentJobRoleFunction
{
    private readonly JobRoleService _jobRoleService;

    public DetachDepartmentJobRoleFunction(JobRoleService jobRoleService)
    {
        _jobRoleService = jobRoleService;
    }

    [Function("DetachDepartmentJobRole")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "departments/{departmentId:guid}/job-roles/{jobRoleId:guid}")]
        HttpRequestData request,
        Guid departmentId,
        Guid jobRoleId,
        CancellationToken cancellationToken)
    {
        var success = await _jobRoleService.DetachFromDepartmentAsync(departmentId, jobRoleId, cancellationToken);
        if (!success)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "NotFound",
                "The requested department or job role was not found.",
                cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
