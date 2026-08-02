using System.Net;
using System.Text.Json;
using HumanOS.Contracts.People;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>PUT /people/{personId}/resume — creates or updates a Person's
/// résumé/CV with the final, HRAdmin-reviewed fields (whether hand-typed
/// or accepted from a ResumeExtractionAgent proposal).</summary>
public sealed class SavePersonResumeFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly PersonResumeService _personResumeService;

    public SavePersonResumeFunction(PersonResumeService personResumeService)
    {
        _personResumeService = personResumeService;
    }

    [Function("SavePersonResume")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "people/{personId:guid}/resume")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        SavePersonResumeRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SavePersonResumeRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidBody", "A request body is required.", cancellationToken);
        }

        var resume = await _personResumeService.UpsertAsync(personId, body, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, resume, cancellationToken: cancellationToken);
    }
}
