using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Returns the single currently in-progress V2 "PDF → CapabilityGraph"
/// run, if any (2026-07-21) — lets the frontend recover/display live
/// progress even after navigating away or reloading, without needing the
/// RunId in the URL, and lets it warn the user before attempting to start
/// a second run (only one is allowed at a time — see
/// <see cref="PdfCapabilityGraphOrchestrator.ActiveRunConflictException"/>).
/// Returns a JSON `null` body when no run is currently Running.
/// </summary>
public sealed class GetActiveCapabilityGraphRunFunction
{
    private readonly PdfCapabilityGraphOrchestrator _orchestrator;

    public GetActiveCapabilityGraphRunFunction(PdfCapabilityGraphOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GetActiveCapabilityGraphRun")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/capability-graph/active")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var status = _orchestrator.GetActiveRun();
        return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
    }
}
