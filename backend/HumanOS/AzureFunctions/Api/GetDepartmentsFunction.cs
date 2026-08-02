using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/departments — the whole org chart for a
/// tenant, flat (frontend builds the tree client-side from
/// ParentDepartmentId). Powers the enterprise console's Organización page.</summary>
public sealed class GetDepartmentsFunction
{
    private readonly DepartmentService _departmentService;

    public GetDepartmentsFunction(DepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [Function("GetDepartments")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/departments")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var departments = await _departmentService.GetByTenantAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, departments, cancellationToken: cancellationToken);
    }
}
