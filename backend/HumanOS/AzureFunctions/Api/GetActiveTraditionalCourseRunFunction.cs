using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Returns the single currently in-progress Traditional Course
/// run, if any — same recovery pattern as GetActiveCapabilityGraphRunFunction.</summary>
public sealed class GetActiveTraditionalCourseRunFunction
{
    private readonly TraditionalCoursePipelineOrchestrator _orchestrator;

    public GetActiveTraditionalCourseRunFunction(TraditionalCoursePipelineOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GetActiveTraditionalCourseRun")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/traditional-course/active")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var status = _orchestrator.GetActiveRun();
        return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
    }
}
