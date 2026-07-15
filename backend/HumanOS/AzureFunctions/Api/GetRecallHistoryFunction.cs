using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetRecallHistoryFunction
{
    private readonly RecallService _recallService;

    public GetRecallHistoryFunction(RecallService recallService)
    {
        _recallService = recallService;
    }

    [Function("GetRecallHistory")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "people/{personId:guid}/recall/{personCapabilityId:guid}")]
        HttpRequestData request,
        Guid personId,
        Guid personCapabilityId,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var limit = int.TryParse(query["limit"], out var l) ? l : 50;

        var history = await _recallService.GetHistoryAsync(personCapabilityId, limit, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, history, cancellationToken: cancellationToken);
    }
}
