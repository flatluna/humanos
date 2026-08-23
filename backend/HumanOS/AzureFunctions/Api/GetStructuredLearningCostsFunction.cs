using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>List view for the Structured Learning cost/time dashboard.</summary>
public sealed class GetStructuredLearningCostsFunction
{
    private readonly StructuredLearningCostService _costService;

    public GetStructuredLearningCostsFunction(StructuredLearningCostService costService)
    {
        _costService = costService;
    }

    [Function("GetStructuredLearningCosts")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning-costs")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var summaries = await _costService.GetSummariesAsync(cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, summaries, cancellationToken: cancellationToken);
    }
}
