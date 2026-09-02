using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/tools — every Tool & Technology
/// in the catalog for a tenant.</summary>
public sealed class GetToolsFunction
{
    private readonly ToolService _toolService;

    public GetToolsFunction(ToolService toolService)
    {
        _toolService = toolService;
    }

    [Function("GetTools")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/tools")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tools = await _toolService.GetByTenantAsync(tenantId, cancellationToken);
        var response = tools.Select(t => new
        {
            toolId = t.ToolId,
            tenantId = t.TenantId,
            name = t.Name,
            description = t.Description,
            isActive = t.IsActive,
            createdDate = t.CreatedDate,
            updatedDate = t.UpdatedDate,
        }).ToList();

        return await FunctionResponseFactory.SuccessResponseAsync(request, response, cancellationToken: cancellationToken);
    }
}
