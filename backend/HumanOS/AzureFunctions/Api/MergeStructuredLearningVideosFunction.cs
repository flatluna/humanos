using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class MergeStructuredLearningVideosRequest
{
    /// <summary>Video illustration IDs, IN THE ORDER they should be
    /// concatenated.</summary>
    public List<Guid> IllustrationIds { get; set; } = [];
}

/// <summary>
/// Merges 2+ already-generated videos for one section, in the caller's
/// chosen order, into a single new video via ffmpeg concat (2026-08-22) —
/// see <see cref="StructuredLearningVideoOrchestrator.MergeAsync"/>. Local
/// processing only, no billed API call, so this runs synchronously.
/// </summary>
public sealed class MergeStructuredLearningVideosFunction
{
    private readonly StructuredLearningVideoOrchestrator _orchestrator;

    public MergeStructuredLearningVideosFunction(StructuredLearningVideoOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("MergeStructuredLearningVideos")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/video/merge")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        MergeStructuredLearningVideosRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<MergeStructuredLearningVideosRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.IllustrationIds.Count < 2)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "At least 2 illustrationIds are required, in the desired order.", cancellationToken);
        }

        try
        {
            var mergedIllustrationId = await _orchestrator.MergeAsync(sectionId, body.IllustrationIds, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new { mergedIllustrationId }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidMergeRequest", ex.Message, cancellationToken);
        }
    }
}
