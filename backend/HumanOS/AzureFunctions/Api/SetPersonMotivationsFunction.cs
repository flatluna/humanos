using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Motivations;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class SetPersonMotivationsFunction
{
    private readonly MotivationService _motivationService;

    public SetPersonMotivationsFunction(MotivationService motivationService)
    {
        _motivationService = motivationService;
    }

    [Function("SetPersonMotivations")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/motivations")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        SetPersonMotivationsRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<SetPersonMotivationsRequest>(
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
                request, HttpStatusCode.BadRequest, "MissingBody", "The request body is required.", cancellationToken);
        }

        try
        {
            var result = await _motivationService.SetPersonMotivationsAsync(
                personId, body.MotivationCodes, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NotFound", ex.Message, cancellationToken);
        }
    }
}
