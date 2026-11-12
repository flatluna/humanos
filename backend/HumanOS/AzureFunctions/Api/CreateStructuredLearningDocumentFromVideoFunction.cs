using System.Net;
using System.Text.Json;
using HumanOS.Services.VideoLearning;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Creates a full Structured Learning document directly from
/// already-synchronized video Engrams (chapter/subchapter/transcript text +
/// representative frame image), produced upstream by EngramsVideos.FrameSync
/// + a chapter/engram grouping step over Azure AI Video Indexer insights.
///
/// This is the "New Learning From Video" counterpart to
/// StartStructuredLearningExtractionFunction (PDF entry point): same target
/// StructuredLearningDocument/Section/Illustration tables, but skips PDF
/// upload, TOC/outline extraction and document classification entirely,
/// since the video pipeline already knows the exact structure. Once this
/// endpoint returns, the resulting sections flow through the SAME
/// entities/connections/engram-card generation endpoints as a PDF-sourced
/// document.
/// </summary>
public sealed class CreateStructuredLearningDocumentFromVideoFunction
{
    private readonly VideoStructuredLearningIngestionService _ingestionService;

    public CreateStructuredLearningDocumentFromVideoFunction(VideoStructuredLearningIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    [Function("CreateStructuredLearningDocumentFromVideo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/video/create")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        VideoIngestionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<VideoIngestionRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", ex.Message, cancellationToken);
        }

        if (body is null || body.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(body.DocumentName))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest",
                "'tenantId' and 'documentName' are required.", cancellationToken);
        }

        if (body.Engrams is not { Count: > 0 })
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingEngrams",
                "'engrams' (from the video frame/transcript sync pipeline) must contain at least one item.",
                cancellationToken);
        }

        try
        {
            var result = await _ingestionService.CreateFromVideoAsync(body, cancellationToken);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "VideoIngestionFailed", ex.Message, cancellationToken);
        }
    }
}
