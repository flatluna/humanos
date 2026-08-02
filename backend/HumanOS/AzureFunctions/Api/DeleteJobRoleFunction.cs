using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>DELETE /job-roles/{jobRoleId} — soft-deletes (deactivates) a
/// Job Role. 409 if people are still assigned to it.</summary>
public sealed class DeleteJobRoleFunction
{
    private readonly JobRoleService _jobRoleService;

    public DeleteJobRoleFunction(JobRoleService jobRoleService)
    {
        _jobRoleService = jobRoleService;
    }

    [Function("DeleteJobRole")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "job-roles/{jobRoleId:guid}")]
        HttpRequestData request,
        Guid jobRoleId,
        CancellationToken cancellationToken)
    {
        try
        {
            var deactivated = await _jobRoleService.DeactivateAsync(jobRoleId, cancellationToken);

            if (!deactivated)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "JobRoleNotFound", "The requested job role was not found.", cancellationToken);
            }

            return request.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "JobRoleHasDependents", ex.Message, cancellationToken);
        }
    }
}
