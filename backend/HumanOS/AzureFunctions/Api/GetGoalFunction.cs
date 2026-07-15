using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetGoalFunction
{
    private readonly GoalService _goalService;

    public GetGoalFunction(GoalService goalService)
    {
        _goalService = goalService;
    }

    [Function("GetGoal")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "goals/{goalId:guid}")]
        HttpRequestData request,
        Guid goalId,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var language = query["language"] ?? "en";

        var goal = await _goalService.GetByIdAsync(goalId, language, cancellationToken);

        if (goal is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "GoalNotFound",
                "The requested goal was not found.",
                cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, goal, cancellationToken: cancellationToken);
    }
}
