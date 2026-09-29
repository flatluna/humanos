using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Deletes one Video Production clip (2026-09-23) — removes both
/// the VideoProductionClip row and its .mp4 blob, mirrors
/// DeleteStructuredLearningVideoFunction.</summary>
public sealed class DeleteVideoProductionClipFunction
{
    private readonly VideoProductionOrchestrator _orchestrator;

    public DeleteVideoProductionClipFunction(VideoProductionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("DeleteVideoProductionClip")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "studio/video-productions/clips/{clipId:guid}")]
        HttpRequestData request,
        Guid clipId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _orchestrator.DeleteClipAsync(clipId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new { deleted = true }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "ClipNotFound", ex.Message, cancellationToken);
        }
    }
}
