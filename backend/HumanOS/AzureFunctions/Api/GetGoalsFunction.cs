using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetGoalsFunction
{
    private readonly GoalService _goalService;

    public GetGoalsFunction(GoalService goalService)
    {
        _goalService = goalService;
    }

    [Function("GetGoals")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "goals")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var language = query["language"] ?? "en";

        var goals = await _goalService.GetActiveAsync(language, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, goals, cancellationToken: cancellationToken);
    }
}
