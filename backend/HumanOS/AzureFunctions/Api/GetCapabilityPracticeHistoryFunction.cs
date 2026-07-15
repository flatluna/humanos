using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetCapabilityPracticeHistoryFunction
{
    private readonly PracticeService _practiceService;

    public GetCapabilityPracticeHistoryFunction(PracticeService practiceService)
    {
        _practiceService = practiceService;
    }

    [Function("GetCapabilityPracticeHistory")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "people/{personId:guid}/practice/{capabilityCode}")]
        HttpRequestData request,
        Guid personId,
        string capabilityCode,
        CancellationToken cancellationToken)
    {
        // TODO: Derive PersonId from the validated Microsoft Entra token.
        var history = await _practiceService.GetByCapabilityCodeAsync(personId, capabilityCode, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, history, cancellationToken: cancellationToken);
    }
}
