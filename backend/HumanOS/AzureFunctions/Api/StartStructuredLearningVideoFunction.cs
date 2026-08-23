using System.Net;
using System.Text.Json;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class StartStructuredLearningVideoRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string Purpose { get; set; } = "Teaching";
    public string? ReferenceImageBase64 { get; set; }
    public string? ReferenceImageContentType { get; set; }
    /// <summary>"veo" (default, $0.30/video) or "omni" (Gemini Omni Flash
    /// Preview, $0.55/video — 2026-08-22).</summary>
    public string VideoModel { get; set; } = "veo";
}

/// <summary>
/// Starts a Google Veo video generation job for one Structured Learning
/// section (2026-08-21) — max 2 videos per section, enforced synchronously
/// by StructuredLearningVideoOrchestrator before any billed API call is made.
/// </summary>
public sealed class StartStructuredLearningVideoFunction
{
    private readonly StructuredLearningVideoOrchestrator _orchestrator;

    public StartStructuredLearningVideoFunction(StructuredLearningVideoOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Function("StartStructuredLearningVideo")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/video/start")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (!_orchestrator.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "GoogleVeoNotConfigured",
                "Google Veo video generation is not configured (missing 'GoogleVeoApiKey' or Data Lake storage).", cancellationToken);
        }

        StartStructuredLearningVideoRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<StartStructuredLearningVideoRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Prompt))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "Prompt is required.", cancellationToken);
        }

        if (!Enum.TryParse<StructuredLearningIllustrationPurpose>(body.Purpose, ignoreCase: true, out var purpose))
        {
            purpose = StructuredLearningIllustrationPurpose.Teaching;
        }

        try
        {
            byte[]? referenceImageBytes = null;
            if (!string.IsNullOrWhiteSpace(body.ReferenceImageBase64))
            {
                referenceImageBytes = Convert.FromBase64String(body.ReferenceImageBase64);
            }

            var status = await _orchestrator.StartAsync(
                sectionId, body.Prompt, purpose, body.VideoModel == "omni" ? "omni" : "veo", referenceImageBytes, body.ReferenceImageContentType, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, status, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidVideoRequest", ex.Message, cancellationToken);
        }
    }
}
