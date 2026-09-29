using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Deletes an entire Video Production (2026-09-24) — the
/// VideoProduction row, all of its clips, and every blob (per-clip .mp4s
/// plus the merged final video). Irreversible, so the frontend must get
/// explicit confirmation from the reviewer before calling this.</summary>
public sealed class DeleteVideoProductionFunction
{
    private readonly VideoProductionOrchestrator _orchestrator;

    public DeleteVideoProductionFunction(VideoProductionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("DeleteVideoProduction")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "studio/video-productions/{videoProductionId:guid}")]
        HttpRequestData request,
        Guid videoProductionId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _orchestrator.DeleteProductionAsync(videoProductionId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new { deleted = true }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ProductionNotFound", ex.Message, cancellationToken);
        }
    }
}
