using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Polls the live status of a Traditional Course run — same
/// polling pattern as GetPdfCapabilityGraphStatusFunction.</summary>
public sealed class GetTraditionalCourseStatusFunction
{
    private readonly TraditionalCoursePipelineOrchestrator _orchestrator;

    public GetTraditionalCourseStatusFunction(TraditionalCoursePipelineOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GetTraditionalCourseStatus")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/traditional-course/{runId:guid}/status")]
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
