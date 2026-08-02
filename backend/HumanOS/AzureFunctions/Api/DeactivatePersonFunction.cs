using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>DELETE /people/{personId} — soft-deactivates an employee
/// (sets Person.IsActive = false). Used by the enterprise console's
/// "Personal" directory to offboard an employee without deleting their
/// history (capabilities, evidence, goals, etc. all remain intact).</summary>
public sealed class DeactivatePersonFunction
{
    private readonly PersonService _personService;

    public DeactivatePersonFunction(PersonService personService)
    {
        _personService = personService;
    }

    [Function("DeactivatePerson")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "people/{personId:guid}")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var deactivated = await _personService.DeactivateAsync(personId, cancellationToken);

        if (deactivated is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonNotFound", "The requested person was not found.", cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.NoContent);
    }
}
