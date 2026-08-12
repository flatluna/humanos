using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Polls the live status of a Step 3 (pedagogy generation) run —
/// same polling pattern as GetStructuredLearningExtractionStatusFunction.</summary>
public sealed class GetStructuredLearningPedagogyStatusFunction
{
    private readonly StructuredLearningPedagogyOrchestrator _orchestrator;

    public GetStructuredLearningPedagogyStatusFunction(StructuredLearningPedagogyOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GetStructuredLearningPedagogyStatus")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/pedagogy/{runId:guid}/status")]
        HttpRequestData request,
        Guid runId,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = _orchestrator.GetStatus(runId);
            return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "RunNotFound", ex.Message, cancellationToken);
        }
    }
}
