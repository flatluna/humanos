using System.Net;
using HumanOS.Services.Family;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api.Family;

/// <summary>
/// GET /family/stages/{stageKey}/capabilities?familyId={guid}
/// Returns global reference capabilities + family-custom capabilities for a stage (e.g. kindergarten).
/// </summary>
public sealed class GetFamilyStageCapabilitiesFunction
{
    private readonly FamilyCapabilityService _capabilityService;

    public GetFamilyStageCapabilitiesFunction(FamilyCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
    }

    [Function("GetFamilyStageCapabilities")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/stages/{stageKey}/capabilities")]
        HttpRequestData request,
        string stageKey,
        CancellationToken cancellationToken)
    {
        Guid? familyId = null;
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        if (Guid.TryParse(query["familyId"], out var parsedId))
        {
            familyId = parsedId;
        }

        var list = await _capabilityService.GetStageCapabilitiesAsync(stageKey, familyId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, list, HttpStatusCode.OK, cancellationToken);
    }
}
