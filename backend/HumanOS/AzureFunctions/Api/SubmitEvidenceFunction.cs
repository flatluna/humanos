using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Evidence;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class SubmitEvidenceFunction
{
    private readonly EvidenceService _evidenceService;

    public SubmitEvidenceFunction(EvidenceService evidenceService)
    {
        _evidenceService = evidenceService;
    }

    [Function("SubmitEvidence")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/evidence")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        SubmitEvidenceRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<SubmitEvidenceRequest>(
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
            var result = await _evidenceService.SubmitAsync(
                personId,
                body.CapabilityId,
                body.PersonProjectId,
                body.Title,
                body.Description,
                body.EvidenceType,
                body.EvidenceUrl,
                body.AssistanceLevel,
                cancellationToken);

            return await FunctionResponseFactory.CreatedResponseAsync(request, result!, cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "NotFound", ex.Message, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "PersonCapabilityNotStarted", ex.Message, cancellationToken);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidAssistanceLevel", ex.Message, cancellationToken);
        }
    }
}
