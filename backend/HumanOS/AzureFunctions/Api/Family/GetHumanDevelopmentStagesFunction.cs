using System.Net;
using HumanOS.Services.Family;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api.Family;

/// <summary>
/// GET /family/stages
/// Returns the global catalog of human development stages / grade levels (shared across all tenants).
/// </summary>
public sealed class GetHumanDevelopmentStagesFunction
{
    private readonly FamilyCapabilityService _capabilityService;

    public GetHumanDevelopmentStagesFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("GetHumanDevelopmentStages")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/stages")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var list = await _capabilityService.ListStagesAsync(cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, list, HttpStatusCode.OK, cancellationToken);
    }
}
