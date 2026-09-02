using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /departments/{departmentId:guid} — returns the full profile for
/// a single Department node.</summary>
public sealed class GetDepartmentFunction
{
    private readonly DepartmentService _departmentService;

    public GetDepartmentFunction(DepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [Function("GetDepartment")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "departments/{departmentId:guid}")]
        HttpRequestData request,
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        var department = await _departmentService.GetResponseByIdAsync(departmentId, cancellationToken);

        if (department is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "DepartmentNotFound", "The requested department was not found.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, department, cancellationToken: cancellationToken);
    }
}
