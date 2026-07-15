using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Capabilities;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class UpdateCapabilityTargetFunction
{
    private readonly PersonCapabilityService _personCapabilityService;

    public UpdateCapabilityTargetFunction(PersonCapabilityService personCapabilityService)
    {
        _personCapabilityService = personCapabilityService;
    }

    [Function("UpdateCapabilityTarget")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "put",
            Route = "people/{personId:guid}/capabilities/{personCapabilityId:guid}/target")]
        HttpRequestData request,
        Guid personId,
        Guid personCapabilityId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        UpdateCapabilityTargetRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<UpdateCapabilityTargetRequest>(
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
            var result = await _personCapabilityService.UpdateTargetLevelAsync(personCapabilityId, body.TargetLevel, cancellationToken);

            if (result is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "PersonCapabilityNotFound", "The requested person capability was not found.", cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidTargetLevel", ex.Message, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "InvalidStatusTransition", ex.Message, cancellationToken);
        }
    }
}
