using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class RegenerateStructuredLearningVideoRequest
{
    public string VideoModel { get; set; } = "veo";
}

/// <summary>Re-runs generation for one existing video using its OWN stored
/// prompt and its OWN first frame as the starting image (2026-08-26) — lets
/// a reviewer re-roll a clip that came out wrong without retyping anything.
/// Does not delete the original video; the caller deletes it separately
/// once the new one looks better.</summary>
public sealed class RegenerateStructuredLearningVideoFunction
{
    private readonly StructuredLearningVideoOrchestrator _orchestrator;

    public RegenerateStructuredLearningVideoFunction(StructuredLearningVideoOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("RegenerateStructuredLearningVideo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/videos/{illustrationId:guid}/regenerate")]
        HttpRequestData request,
        Guid illustrationId,
        CancellationToken cancellationToken)
    {
        RegenerateStructuredLearningVideoRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<RegenerateStructuredLearningVideoRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken) ?? new RegenerateStructuredLearningVideoRequest();
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        try
        {
            var status = await _orchestrator.RegenerateAsync(illustrationId, body.VideoModel, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "RegenerateFailed", ex.Message, cancellationToken);
        }
    }
}
