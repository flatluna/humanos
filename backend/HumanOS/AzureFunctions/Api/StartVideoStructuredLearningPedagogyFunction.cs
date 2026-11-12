using System.Net;
using System.Text.Json;
using HumanOS.Services.VideoLearning;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Starts Step 3 (pedagogy generation) for a VIDEO-sourced Structured
/// Learning Document — completely separate endpoint from
/// <see cref="StartStructuredLearningPedagogyFunction"/> (the traditional
/// PDF flow), delegating to <see cref="VideoStructuredLearningPedagogyOrchestrator"/>
/// so the real extracted video frames are reused as each Teaching step's
/// illustration instead of generating new AI images. New, additive endpoint
/// — does not affect the traditional pedagogy endpoint/orchestrator in any way.
/// </summary>
public sealed class StartVideoStructuredLearningPedagogyFunction
{
    private readonly VideoStructuredLearningPedagogyOrchestrator _orchestrator;

    public StartVideoStructuredLearningPedagogyFunction(VideoStructuredLearningPedagogyOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public sealed class StartRequest
    {
        public Guid StructuredLearningDocumentId { get; set; }

        public string? LanguageCode { get; set; }

        /// <summary>When true, reprocesses EVERY engram (not just
        /// non-Completed ones).</summary>
        public bool ForceRegenerateAll { get; set; }

        public bool? UsePerplexitySearch { get; set; }

        /// <summary>Optional 1-based inclusive engram range — same
        /// semantics as the traditional endpoint's field of the same name.</summary>
        public int? FromEngramNumber { get; set; }

        public int? ToEngramNumber { get; set; }
    }

    [Function("StartVideoStructuredLearningPedagogy")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/video/pedagogy/start")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_orchestrator.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StudioAgentsNotConfigured",
                "The Structured Learning pedagogy agent is not yet configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        var body = await JsonSerializer.DeserializeAsync<StartRequest>(
            request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);

        if (body is null || body.StructuredLearningDocumentId == Guid.Empty)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRequest",
                "'structuredLearningDocumentId' is required.", cancellationToken);
        }

        var languageCode = string.IsNullOrWhiteSpace(body.LanguageCode) ? "es" : body.LanguageCode;

        try
        {
            var status = _orchestrator.Start(
                body.StructuredLearningDocumentId,
                languageCode,
                body.ForceRegenerateAll,
                body.UsePerplexitySearch,
                body.FromEngramNumber,
                body.ToEngramNumber);
            return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
        }
        catch (VideoStructuredLearningPedagogyOrchestrator.ActiveRunConflictException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.Conflict, "ActiveRunConflict", ex.Message, cancellationToken);
        }
    }
}

/// <summary>Returns the currently in-progress video-pedagogy run for a
/// given document, if any — same recovery pattern as
/// GetActiveStructuredLearningPedagogyRunFunction, so the run progress page
/// can find its way back to an already-started run even after the browser
/// reloaded/navigated away and lost the in-memory RunId (2026-11-06).</summary>
public sealed class GetActiveVideoStructuredLearningPedagogyRunFunction
{
    private readonly VideoStructuredLearningPedagogyOrchestrator _orchestrator;

    public GetActiveVideoStructuredLearningPedagogyRunFunction(VideoStructuredLearningPedagogyOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GetActiveVideoStructuredLearningPedagogyRun")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/video/pedagogy/active/{documentId:guid}")]
        HttpRequestData request,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var status = _orchestrator.GetActiveRun(documentId);
        return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
    }
}

/// <summary>Polls a video-pedagogy run started by
/// <see cref="StartVideoStructuredLearningPedagogyFunction"/>.</summary>
public sealed class GetVideoStructuredLearningPedagogyStatusFunction
{
    private readonly VideoStructuredLearningPedagogyOrchestrator _orchestrator;

    public GetVideoStructuredLearningPedagogyStatusFunction(VideoStructuredLearningPedagogyOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("GetVideoStructuredLearningPedagogyStatus")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/video/pedagogy/{runId:guid}/status")]
        HttpRequestData request,
        Guid runId,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = _orchestrator.GetStatus(runId);
            return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "RunNotFound", ex.Message, cancellationToken);
        }
    }
}
