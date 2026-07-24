using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Capabilities;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class StartCapabilityDevelopmentFunction
{
    private readonly PersonCapabilityService _personCapabilityService;

    public StartCapabilityDevelopmentFunction(PersonCapabilityService personCapabilityService)
    {
        _personCapabilityService = personCapabilityService;
    }

    [Function("StartCapabilityDevelopment")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/capabilities/{capabilityId:guid}")]
        HttpRequestData request,
        Guid personId,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        StartCapabilityDevelopmentRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<StartCapabilityDevelopmentRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        var targetLevel = body?.TargetLevel ?? 5;
        var selfAssessedLevel = body?.SelfAssessedLevel;

        try
        {
            var result = await _personCapabilityService.StartAsync(
                personId, capabilityId, targetLevel, selfAssessedLevel, cancellationToken);

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
                request, HttpStatusCode.Conflict, "CapabilityAlreadyStarted", ex.Message, cancellationToken);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidTargetLevel", ex.Message, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidSelfAssessedLevel", ex.Message, cancellationToken);
        }
    }
}
