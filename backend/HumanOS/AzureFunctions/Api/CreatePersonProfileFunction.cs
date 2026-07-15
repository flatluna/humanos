using System.Net;
using System.Text.Json;
using HumanOS.Contracts.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class CreatePersonProfileFunction
{
    private readonly PersonProfileService _personProfileService;

    public CreatePersonProfileFunction(PersonProfileService personProfileService)
    {
        _personProfileService = personProfileService;
    }

    [Function("CreatePersonProfile")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
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

            var profile = await _personProfileService.CreateAsync(
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

            var response = request.CreateResponse(HttpStatusCode.Created);
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
        catch (KeyNotFoundException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonNotFound", "The requested person was not found.", cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "PersonProfileAlreadyExists", "A profile already exists for this person.", cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidLanguage", ex.Message, cancellationToken);
        }
    }
}
