using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>DELETE /departments/{departmentId} — soft-deletes
/// (deactivates) a Department. 409 if it still has active
/// sub-departments, job roles, or assigned people.</summary>
public sealed class DeleteDepartmentFunction
{
    private readonly DepartmentService _departmentService;

    public DeleteDepartmentFunction(DepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [Function("DeleteDepartment")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "departments/{departmentId:guid}")]
        HttpRequestData request,
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var deactivated = await _departmentService.DeactivateAsync(departmentId, cancellationToken);

            if (!deactivated)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "DepartmentNotFound", "The requested department was not found.", cancellationToken);
            }

            return request.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "DepartmentHasDependents", ex.Message, cancellationToken);
        }
    }
}
