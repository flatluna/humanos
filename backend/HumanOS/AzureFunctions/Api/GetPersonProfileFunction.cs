using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetPersonProfileFunction
{
    private readonly PersonProfileService _personProfileService;

    public GetPersonProfileFunction(
        PersonProfileService personProfileService)
    {
        _personProfileService = personProfileService;
    }

    [Function("GetPersonProfile")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "people/{personId:guid}/profile")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        var profile = await _personProfileService.GetByPersonIdAsync(
            personId,
            cancellationToken);

        if (profile is null)
        {
            var notFound = request.CreateResponse(
                HttpStatusCode.NotFound);

            await notFound.WriteAsJsonAsync(
                new
                {
                    error = "PersonProfileNotFound",
                    message = "The requested person profile was not found."
                },
                cancellationToken);

            return notFound;
        }

        var response = request.CreateResponse(HttpStatusCode.OK);

        await response.WriteAsJsonAsync(
            new
            {
                profile.PersonProfileId,
                profile.PersonId,
                profile.FirstName,
                profile.LastName,
                profile.DisplayName,
                profile.PhoneNumber,
                profile.PreferredLanguage,
                profile.CountryCode,
                profile.TimeZone,
                profile.ProfilePhotoUrl,
                profile.DateOfBirth,
                profile.Occupation,
                profile.Company,
                profile.Biography,
                profile.CreatedDate,
                profile.UpdatedDate
            },
            cancellationToken);

        return response;
    }
}
