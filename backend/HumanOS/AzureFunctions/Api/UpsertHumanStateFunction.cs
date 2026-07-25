using System.Net;
using System.Text.Json;
using HumanOS.Contracts.HumanState;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>POST /people/{personId}/human-state — records/updates today's
/// Human State check-in (focus/energy/purpose/confidence, each 0-100).</summary>
public sealed class UpsertHumanStateFunction
{
    private readonly HumanStateService _humanStateService;

    public UpsertHumanStateFunction(HumanStateService humanStateService)
    {
        _humanStateService = humanStateService;
    }

    [Function("UpsertHumanState")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/human-state")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        UpsertHumanStateRequest? body;

        try
        {
            body = await JsonSerializer.DeserializeAsync<UpsertHumanStateRequest>(
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

        var result = await _humanStateService.UpsertTodayAsync(
            personId, body.Energy, body.Focus, body.Purpose, body.Confidence, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
    }
}
