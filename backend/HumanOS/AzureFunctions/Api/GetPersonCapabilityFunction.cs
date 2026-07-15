using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetPersonCapabilityFunction
{
    private readonly PersonCapabilityService _personCapabilityService;

    public GetPersonCapabilityFunction(PersonCapabilityService personCapabilityService)
    {
        _personCapabilityService = personCapabilityService;
    }

    [Function("GetPersonCapability")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "people/{personId:guid}/capabilities/{personCapabilityId:guid}")]
        HttpRequestData request,
        Guid personId,
        Guid personCapabilityId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        var all = await _personCapabilityService.GetByPersonAsync(personId, cancellationToken);

        var capability = all.FirstOrDefault(c => c.PersonCapabilityId == personCapabilityId);

        if (capability is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "PersonCapabilityNotFound",
                "The requested person capability was not found.",
                cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, capability, cancellationToken: cancellationToken);
    }
}
