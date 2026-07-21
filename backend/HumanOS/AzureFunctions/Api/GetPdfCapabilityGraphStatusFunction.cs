using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Polls the live status of a V2 "PDF → CapabilityGraph" run: Running
/// (with a short progress description), Completed (with the final
/// CapabilityId/CapabilityGraphId/counts), or Failed. Same polling
/// pattern as GetCapabilityCreationStatusFunction (V1).
/// </summary>
public sealed class GetPdfCapabilityGraphStatusFunction
{
    private readonly PdfCapabilityGraphOrchestrator _orchestrator;

    public GetPdfCapabilityGraphStatusFunction(PdfCapabilityGraphOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GetPdfCapabilityGraphStatus")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "studio/capability-graph/{runId:guid}/status")]
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
