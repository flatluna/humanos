using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetPersonCapabilitiesFunction
{
    private readonly PersonCapabilityService _personCapabilityService;

    public GetPersonCapabilitiesFunction(PersonCapabilityService personCapabilityService)
    {
        _personCapabilityService = personCapabilityService;
    }

    [Function("GetPersonCapabilities")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "people/{personId:guid}/capabilities")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        var capabilities = await _personCapabilityService.GetByPersonAsync(personId, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, capabilities, cancellationToken: cancellationToken);
    }
}
