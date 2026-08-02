using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /job-roles/{jobRoleId} — a single Job Role ("puesto")
/// with its full job-description fields, for the Roles y Puestos
/// full-page editor (as opposed to GetJobRolesFunction's list view).</summary>
public sealed class GetJobRoleByIdFunction
{
    private readonly JobRoleService _jobRoleService;

    public GetJobRoleByIdFunction(JobRoleService jobRoleService)
    {
        _jobRoleService = jobRoleService;
    }

    [Function("GetJobRoleById")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "job-roles/{jobRoleId:guid}")]
        HttpRequestData request,
        Guid jobRoleId,
        CancellationToken cancellationToken)
    {
        var jobRole = await _jobRoleService.GetResponseByIdAsync(jobRoleId, cancellationToken);

        if (jobRole is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "JobRoleNotFound", "The requested job role was not found.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, jobRole, cancellationToken: cancellationToken);
    }
}
