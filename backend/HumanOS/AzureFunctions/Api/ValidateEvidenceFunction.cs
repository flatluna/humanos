using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Evidence;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class ValidateEvidenceFunction
{
    private readonly EvidenceService _evidenceService;

    public ValidateEvidenceFunction(EvidenceService evidenceService)
    {
        _evidenceService = evidenceService;
    }

    [Function("ValidateEvidence")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "put",
            Route = "evidence/{evidenceId:guid}/validate")]
        HttpRequestData request,
        Guid evidenceId,
        CancellationToken cancellationToken)
    {
        ValidateEvidenceRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<ValidateEvidenceRequest>(
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
            var result = await _evidenceService.ValidateAsync(
                evidenceId,
                body.ValidationStatus,
                body.ValidationFeedback,
                cancellationToken);

            if (result is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "EvidenceNotFound", "The requested evidence was not found.", cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidValidationStatus", ex.Message, cancellationToken);
        }
    }
}
