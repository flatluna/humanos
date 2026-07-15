using System.Net;
using System.Text.Json;
using HumanOS.Contracts.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class UpdatePersonProfileFunction
{
    private readonly PersonProfileService _personProfileService;

    public UpdatePersonProfileFunction(PersonProfileService personProfileService)
    {
        _personProfileService = personProfileService;
    }

    [Function("UpdatePersonProfile")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "put",
            Route = "people/{personId:guid}/profile")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        try
        {
            var req = await JsonSerializer.DeserializeAsync<UpsertPersonProfileRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (req is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "RequestBodyRequired", "Request body is required.", cancellationToken);
            }

            var profile = await _personProfileService.UpdateAsync(
                personId,
                req.FirstName,
                req.LastName,
                req.DisplayName,
                req.PhoneNumber,
                req.PreferredLanguage ?? "en",
                req.CountryCode,
                req.TimeZone,
                req.ProfilePhotoUrl,
                req.DateOfBirth,
                req.Occupation,
                req.Company,
                req.Biography,
                cancellationToken);

            if (profile is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "PersonProfileNotFound", "The requested person profile was not found.", cancellationToken);
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
        catch (ArgumentException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidLanguage", ex.Message, cancellationToken);
        }
    }
}
