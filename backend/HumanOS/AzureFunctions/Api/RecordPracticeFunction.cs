using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Practice;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class RecordPracticeFunction
{
    private readonly PracticeService _practiceService;

    public RecordPracticeFunction(PracticeService practiceService)
    {
        _practiceService = practiceService;
    }

    [Function("RecordPractice")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/practice")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        RecordPracticeRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<RecordPracticeRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "RequestBodyRequired", "A request body is required.", cancellationToken);
        }

        try
        {
            var result = await _practiceService.RecordAsync(
                body.PersonCapabilityId,
                body.PracticeType,
                body.AssistanceLevel,
                body.PersonReflection,
                body.LanguageCode,
                cancellationToken);

            return await FunctionResponseFactory.CreatedResponseAsync(request, result!, cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonCapabilityNotFound", ex.Message, cancellationToken);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidAssistanceLevel", ex.Message, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidLanguage", ex.Message, cancellationToken);
        }
    }
}
