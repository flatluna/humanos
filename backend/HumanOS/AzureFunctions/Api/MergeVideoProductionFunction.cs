using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class MergeVideoProductionRequest
{
    /// <summary>Clip IDs, IN THE ORDER they should be concatenated
    /// (2026-09-23, mirrors MergeStructuredLearningVideosRequest).</summary>
    public List<Guid> ClipIds { get; set; } = [];
}

/// <summary>Concatenates the caller-selected clips of a Video Production,
/// in the caller's chosen order, into ONE final video — local ffmpeg
/// merge, no billed API call.</summary>
public sealed class MergeVideoProductionFunction
{
    private readonly VideoProductionOrchestrator _orchestrator;

    public MergeVideoProductionFunction(VideoProductionOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("MergeVideoProduction")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/video-productions/{videoProductionId:guid}/merge")]
        HttpRequestData request,
        Guid videoProductionId,
        CancellationToken cancellationToken)
    {
        MergeVideoProductionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<MergeVideoProductionRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.ClipIds.Count < 2)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "At least 2 clipIds are required, in the desired order.", cancellationToken);
        }

        try
        {
            var production = await _orchestrator.MergeAsync(videoProductionId, body.ClipIds, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, production, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidMergeRequest", ex.Message, cancellationToken);
        }
    }
}

