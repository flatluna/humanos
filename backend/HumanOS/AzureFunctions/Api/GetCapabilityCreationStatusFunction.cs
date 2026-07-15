using System.Net;
using HumanOS.Agentic.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Polls the live status of a Human OS Studio capability-creation run:
/// Running (with progress), PendingGate (with the gate's payload),
/// Completed, or Failed. The frontend calls this repeatedly (same
/// polling pattern it already uses against the mock APIs) instead of
/// blocking on /start or /respond, which now return immediately.
/// </summary>
public sealed class GetCapabilityCreationStatusFunction
{
    private readonly CapabilityCreationOrchestrator _orchestrator;

    public GetCapabilityCreationStatusFunction(CapabilityCreationOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GetCapabilityCreationStatus")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "studio/capability-creation/{runId:guid}/status")]
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
