using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetVideoProductionClipLastFrameResponse
{
    public string Base64 { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
}

/// <summary>Returns the last frame of an already-generated Video Production
/// clip as a JPEG (2026-09-23) — used by the wizard to auto-carry visual
/// continuity into the next clip's starting image, and to show the
/// reviewer exactly where the previous clip left off.</summary>
public sealed class GetVideoProductionClipLastFrameFunction
{
    private readonly VideoProductionOrchestrator _orchestrator;

    public GetVideoProductionClipLastFrameFunction(VideoProductionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GetVideoProductionClipLastFrame")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/video-productions/clips/{clipId:guid}/last-frame")]
        HttpRequestData request,
        Guid clipId,
        CancellationToken cancellationToken)
    {
        try
        {
            var jpegBytes = await _orchestrator.GetLastFrameAsync(clipId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(
                request,
                new GetVideoProductionClipLastFrameResponse { Base64 = Convert.ToBase64String(jpegBytes) },
                cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "LastFrameExtractionFailed", ex.Message, cancellationToken);
        }
    }
}
