using System.Net;
using System.Text.Json;
using HumanOS.Contracts.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class UpsertPersonAcademicPreferencesFunction
{
    private readonly PersonAcademicPreferencesService _service;

    public UpsertPersonAcademicPreferencesFunction(PersonAcademicPreferencesService service)
    {
        _service = service;
    }

    [Function("UpsertPersonAcademicPreferences")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "people/{personId:guid}/academic-preferences")] HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var body = await JsonSerializer.DeserializeAsync<UpsertPersonAcademicPreferencesRequest>(
            request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "RequestBodyRequired", "Request body is required.", cancellationToken);
        }

        try
        {
            var profile = await _service.UpsertAsync(personId, body, cancellationToken);
            return await GetPersonAcademicPreferencesFunction.WriteResponseAsync(request, profile, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonNotFound", "The requested person was not found.", cancellationToken);
        }
    }
}
