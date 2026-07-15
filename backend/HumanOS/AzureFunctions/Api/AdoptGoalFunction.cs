using System.Net;
using System.Text.Json;
using HumanOS.Contracts.Goals;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class AdoptGoalFunction
{
    private readonly GoalService _goalService;

    public AdoptGoalFunction(GoalService goalService)
    {
        _goalService = goalService;
    }

    [Function("AdoptGoal")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/goals/{goalId:guid}")]
        HttpRequestData request,
        Guid personId,
        Guid goalId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        AdoptGoalRequest? body = null;

        if (request.Body.Length > 0)
        {
            try
            {
                body = await JsonSerializer.DeserializeAsync<AdoptGoalRequest>(
                    request.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken);
            }
            catch (JsonException)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
            }
        }

        try
        {
            var result = await _goalService.AdoptAsync(personId, goalId, body?.TargetDate, cancellationToken);

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
                request, HttpStatusCode.Conflict, "GoalAlreadyAdopted", ex.Message, cancellationToken);
        }
    }
}
