using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GetStructuredLearningVideoLastFrameResponse
{
    public string Base64 { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
}

/// <summary>Returns the last frame of an already-generated video as a JPEG
/// (2026-08-25) — used by the multi-clip video wizard to auto-carry visual
/// continuity into the next clip's starting image.</summary>
public sealed class GetStructuredLearningVideoLastFrameFunction
{
    private readonly StructuredLearningVideoOrchestrator _orchestrator;

    public GetStructuredLearningVideoLastFrameFunction(StructuredLearningVideoOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GetStructuredLearningVideoLastFrame")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/videos/{illustrationId:guid}/last-frame")]
        HttpRequestData request,
        Guid illustrationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var jpegBytes = await _orchestrator.GetLastFrameAsync(illustrationId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(
                request,
                new GetStructuredLearningVideoLastFrameResponse { Base64 = Convert.ToBase64String(jpegBytes) },
                cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "LastFrameExtractionFailed", ex.Message, cancellationToken);
        }
    }
}
