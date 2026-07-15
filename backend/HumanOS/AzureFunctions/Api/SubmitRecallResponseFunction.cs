using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Recall;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class SubmitRecallResponseFunction
{
    private readonly RecallService _recallService;

    public SubmitRecallResponseFunction(RecallService recallService)
    {
        _recallService = recallService;
    }

    [Function("SubmitRecallResponse")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "put",
            Route = "people/{personId:guid}/recall/{recallAttemptId:guid}")]
        HttpRequestData request,
        Guid personId,
        Guid recallAttemptId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        SubmitRecallResponseRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<SubmitRecallResponseRequest>(
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
            var result = await _recallService.SubmitResponseAsync(
                recallAttemptId,
                body.PersonResponse,
                null,
                body.ConfidenceScore,
                body.AssistanceLevel,
                cancellationToken);

            if (result is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "RecallAttemptNotFound", "The requested recall attempt was not found.", cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidValue", ex.Message, cancellationToken);
        }
    }
}
