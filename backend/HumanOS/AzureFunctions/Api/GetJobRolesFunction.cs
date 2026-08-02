using System.Net;
using System.Web;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/job-roles?departmentId={id} — every
/// Job Role ("puesto") for a tenant, optionally filtered to one
/// Department. Powers the enterprise console's Roles/Puestos page.</summary>
public sealed class GetJobRolesFunction
{
    private readonly JobRoleService _jobRoleService;

    public GetJobRolesFunction(JobRoleService jobRoleService)
    {
        _jobRoleService = jobRoleService;
    }

    [Function("GetJobRoles")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/job-roles")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(request.Url.Query);
        Guid? departmentId = Guid.TryParse(query["departmentId"], out var parsed) ? parsed : null;

        var jobRoles = await _jobRoleService.GetByTenantAsync(tenantId, departmentId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, jobRoles, cancellationToken: cancellationToken);
    }
}
