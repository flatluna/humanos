using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class VideoKeepRange
{
    public double Start { get; set; }
    public double End { get; set; }
}

public sealed class CutStructuredLearningVideoSegmentsRequest
{
    /// <summary>Disjoint [Start,End) ranges, IN ORDER, to KEEP — everything
    /// else (e.g. a chunk the reviewer marked for deletion in the middle
    /// of the clip) is cut out.</summary>
    public List<VideoKeepRange> KeepRanges { get; set; } = [];
}

/// <summary>
/// Cuts a chunk out of the middle of one video in place (2026-08-22) — see
/// <see cref="StructuredLearningVideoOrchestrator.CutSegmentsAsync"/>.
/// </summary>
public sealed class CutStructuredLearningVideoSegmentsFunction
{
    private readonly StructuredLearningVideoOrchestrator _orchestrator;

    public CutStructuredLearningVideoSegmentsFunction(StructuredLearningVideoOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("CutStructuredLearningVideoSegments")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/videos/{illustrationId:guid}/cut")]
        HttpRequestData request,
        Guid illustrationId,
        CancellationToken cancellationToken)
    {
        CutStructuredLearningVideoSegmentsRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CutStructuredLearningVideoSegmentsRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || body.KeepRanges.Count == 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "At least one keepRange is required.", cancellationToken);
        }

        try
        {
            await _orchestrator.CutSegmentsAsync(
                illustrationId, body.KeepRanges.Select(r => (r.Start, r.End)).ToList(), cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new { cut = true }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidCutRequest", ex.Message, cancellationToken);
        }
    }
}
