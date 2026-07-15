using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetRecentPracticeFunction
{
    private readonly PracticeService _practiceService;

    public GetRecentPracticeFunction(PracticeService practiceService)
    {
        _practiceService = practiceService;
    }

    [Function("GetRecentPractice")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "people/{personId:guid}/practice")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var daysBack = int.TryParse(query["days"], out var d) ? d : 30;

        var recent = await _practiceService.GetRecentAsync(personId, daysBack, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, recent, cancellationToken: cancellationToken);
    }
}
