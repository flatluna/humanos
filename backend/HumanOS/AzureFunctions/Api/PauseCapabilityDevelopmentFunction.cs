using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class PauseCapabilityDevelopmentFunction
{
    private readonly PersonCapabilityService _personCapabilityService;

    public PauseCapabilityDevelopmentFunction(PersonCapabilityService personCapabilityService)
    {
        _personCapabilityService = personCapabilityService;
    }

    [Function("PauseCapabilityDevelopment")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/capabilities/{personCapabilityId:guid}/pause")]
        HttpRequestData request,
        Guid personId,
        Guid personCapabilityId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        try
        {
            var result = await _personCapabilityService.PauseAsync(personCapabilityId, cancellationToken);

            if (result is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "PersonCapabilityNotFound", "The requested person capability was not found.", cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "InvalidStatusTransition", ex.Message, cancellationToken);
        }
    }
}
