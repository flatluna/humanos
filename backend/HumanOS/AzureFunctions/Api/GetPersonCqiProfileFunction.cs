using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>GET /people/{personId}/cqi-profile — the employee's own CQI
/// Workspace profile. Always returns 200 (Profile null if none uploaded
/// yet) so the frontend can always render the upload prompt.</summary>
public sealed class GetPersonCqiProfileFunction
{
    private readonly PersonCqiProfileService _service;

    public GetPersonCqiProfileFunction(PersonCqiProfileService service)
    {
        _service = service;
    }

    [Function("GetPersonCqiProfile")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/cqi-profile")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var profile = await _service.GetByPersonIdAsync(personId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, profile, cancellationToken: cancellationToken);
    }
}
