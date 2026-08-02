using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /tenants/{tenantId}/people — the full people directory for
/// a tenant (Department/JobRole/Manager assignments + OrgRole), powering
/// the enterprise console's assignment pickers and people list.</summary>
public sealed class GetTenantPeopleFunction
{
    private readonly PersonService _personService;

    public GetTenantPeopleFunction(PersonService personService)
    {
        _personService = personService;
    }

    [Function("GetTenantPeople")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenants/{tenantId:guid}/people")]
        HttpRequestData request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var people = await _personService.GetTenantDirectoryAsync(tenantId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, people, cancellationToken: cancellationToken);
    }
}
