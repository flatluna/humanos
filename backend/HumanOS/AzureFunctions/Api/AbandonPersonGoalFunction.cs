using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class AbandonPersonGoalFunction
{
    private readonly GoalService _goalService;

    public AbandonPersonGoalFunction(GoalService goalService)
    {
        _goalService = goalService;
    }

    [Function("AbandonPersonGoal")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "people/{personId:guid}/goals/{personGoalId:guid}/abandon")]
        HttpRequestData request,
        Guid personId,
        Guid personGoalId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        try
        {
            var result = await _goalService.AbandonAsync(personGoalId, cancellationToken);

            if (result is null)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.NotFound, "PersonGoalNotFound", "The requested person goal was not found.", cancellationToken);
            }

            return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "InvalidStatusTransition", ex.Message, cancellationToken);
        }
    }
}
