using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Returns the single currently in-progress Structured Learning
/// pedagogy (Step 3) run, if any \u2014 same recovery pattern as
/// GetActiveTraditionalCourseRunFunction, so the global "generating" banner
/// can find its way back even after the browser lost the run's own RunId
/// (2026-08-30).</summary>
public sealed class GetActiveStructuredLearningPedagogyRunFunction
{
    private readonly StructuredLearningPedagogyOrchestrator _orchestrator;

    public GetActiveStructuredLearningPedagogyRunFunction(StructuredLearningPedagogyOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GetActiveStructuredLearningPedagogyRun")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/pedagogy/active")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var status = _orchestrator.GetActiveRun();
        return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
    }
}
