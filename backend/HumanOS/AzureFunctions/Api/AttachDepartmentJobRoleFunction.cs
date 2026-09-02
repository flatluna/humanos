using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// POST /departments/{departmentId:guid}/job-roles/{jobRoleId:guid} —
/// Attaches an existing Job Role to a Department.
/// </summary>
public sealed class AttachDepartmentJobRoleFunction
{
    private readonly JobRoleService _jobRoleService;

    public AttachDepartmentJobRoleFunction(JobRoleService jobRoleService)
    {
        _jobRoleService = jobRoleService;
    }

    [Function("AttachDepartmentJobRole")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "departments/{departmentId:guid}/job-roles/{jobRoleId:guid}")]
        HttpRequestData request,
        Guid departmentId,
        Guid jobRoleId,
        CancellationToken cancellationToken)
    {
        var success = await _jobRoleService.AttachToDepartmentAsync(departmentId, jobRoleId, cancellationToken);
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
