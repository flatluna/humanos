using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Detail view for one Structured Learning Document's cost/time breakdown per step.</summary>
public sealed class GetStructuredLearningCostDetailFunction
{
    private readonly StructuredLearningCostService _costService;

    public GetStructuredLearningCostDetailFunction(StructuredLearningCostService costService)
    {
        _costService = costService;
    }

    [Function("GetStructuredLearningCostDetail")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning-costs/{documentId:guid}")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var detail = await _costService.GetDetailAsync(documentId, cancellationToken);
        if (detail is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "not_found", "No se encontró este documento.", cancellationToken);
        }

        return await FunctionResponseFactory.SuccessResponseAsync(request, detail, cancellationToken: cancellationToken);
    }
}
