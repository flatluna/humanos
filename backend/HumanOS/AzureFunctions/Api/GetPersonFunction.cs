using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetPersonFunction
{
    private readonly PersonService _personService;

    public GetPersonFunction(PersonService personService)
    {
        _personService = personService;
    }

    [Function("GetPerson")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "people/{personId:guid}")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        var person = await _personService.GetByIdAsync(personId, cancellationToken);

        if (person is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "PersonNotFound",
                "The requested person was not found.",
                cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, person, cancellationToken: cancellationToken);
    }
}
