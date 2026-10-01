using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetPersonAcademicPreferencesFunction
{
    private readonly PersonAcademicPreferencesService _service;

    public GetPersonAcademicPreferencesFunction(PersonAcademicPreferencesService service)
    {
        _service = service;
    }

    [Function("GetPersonAcademicPreferences")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "people/{personId:guid}/academic-preferences")] HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var profile = await _service.GetAsync(personId, cancellationToken);
        if (profile is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "AcademicPreferencesNotFound",
                "No academic preferences have been saved for this person.", cancellationToken);
        }

        return await WriteResponseAsync(request, profile, cancellationToken);
    }

    internal static async Task<HttpResponseData> WriteResponseAsync(
        HttpRequestData request,
        HumanOS.Models.People.PersonAcademicPreferences profile,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            profile.PersonAcademicPreferencesId,
            profile.PersonId,
            AcademicInterests = System.Text.Json.JsonSerializer.Deserialize<string[]>(profile.AcademicInterestsJson) ?? [],
            LearningPreferences = System.Text.Json.JsonSerializer.Deserialize<string[]>(profile.LearningPreferencesJson) ?? [],
            AcademicMotivations = System.Text.Json.JsonSerializer.Deserialize<string[]>(profile.AcademicMotivationsJson) ?? [],
            GrowthAreas = System.Text.Json.JsonSerializer.Deserialize<string[]>(profile.GrowthAreasJson) ?? [],
            profile.Frustrations,
            profile.MainChallenges,
            profile.ImportantValues,
            profile.CreatedDate,
            profile.UpdatedDate,
        }, cancellationToken);
        return response;
    }
}
