using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Recall;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class CreateRecallAttemptFunction
{
    private readonly RecallService _recallService;

    public CreateRecallAttemptFunction(RecallService recallService)
    {
        _recallService = recallService;
    }

    [Function("CreateRecallAttempt")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/recall")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        CreateRecallAttemptRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<CreateRecallAttemptRequest>(
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
            var result = await _recallService.CreateAttemptAsync(
                body.PersonCapabilityId,
                body.RecallPrompt,
                body.LanguageCode,
                cancellationToken);

            return await FunctionResponseFactory.CreatedResponseAsync(request, result!, cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonCapabilityNotFound", ex.Message, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidLanguage", ex.Message, cancellationToken);
        }
    }
}
