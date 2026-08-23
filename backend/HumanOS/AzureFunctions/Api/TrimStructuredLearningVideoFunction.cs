using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class TrimStructuredLearningVideoRequest
{
    public double StartSeconds { get; set; }
    public double? EndSeconds { get; set; }
}

/// <summary>
/// Trims one video's start/end in place (2026-08-22, frame-accurate via
/// ffmpeg re-encode) — see <see cref="StructuredLearningVideoOrchestrator.TrimAsync"/>.
/// Same StoragePath/illustration id afterward, just shorter content.
/// </summary>
public sealed class TrimStructuredLearningVideoFunction
{
    private readonly StructuredLearningVideoOrchestrator _orchestrator;

    public TrimStructuredLearningVideoFunction(StructuredLearningVideoOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("TrimStructuredLearningVideo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/videos/{illustrationId:guid}/trim")]
        HttpRequestData request,
        Guid illustrationId,
        CancellationToken cancellationToken)
    {
        TrimStructuredLearningVideoRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<TrimStructuredLearningVideoRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "startSeconds is required.", cancellationToken);
        }

        try
        {
            await _orchestrator.TrimAsync(illustrationId, body.StartSeconds, body.EndSeconds, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new { trimmed = true }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidTrimRequest", ex.Message, cancellationToken);
        }
    }
}
