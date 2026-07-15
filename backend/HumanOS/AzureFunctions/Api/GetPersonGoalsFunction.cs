using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetPersonGoalsFunction
{
    private readonly GoalService _goalService;

    public GetPersonGoalsFunction(GoalService goalService)
    {
        _goalService = goalService;
    }

    [Function("GetPersonGoals")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "people/{personId:guid}/goals")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var language = query["language"] ?? "en";

        var goals = await _goalService.GetPersonGoalsAsync(personId, language, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, goals, cancellationToken: cancellationToken);
    }
}
