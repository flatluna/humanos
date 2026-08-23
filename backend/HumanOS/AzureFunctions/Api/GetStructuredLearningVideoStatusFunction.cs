using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Polls one Veo video generation run started by
/// StartStructuredLearningVideoFunction.</summary>
public sealed class GetStructuredLearningVideoStatusFunction
{
    private readonly StructuredLearningVideoOrchestrator _orchestrator;

    public GetStructuredLearningVideoStatusFunction(StructuredLearningVideoOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GetStructuredLearningVideoStatus")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/video/{runId:guid}/status")]
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
                request, System.Net.HttpStatusCode.NotFound, "RunNotFound", ex.Message, cancellationToken);
        }
    }
}
