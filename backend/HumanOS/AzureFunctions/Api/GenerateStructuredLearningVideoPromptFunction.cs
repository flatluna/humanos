using System.Net;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class GenerateStructuredLearningVideoPromptRequest
{
    /// <summary>User-authored one-sentence learning objective (2026-08-25) —
    /// the authoritative anchor for what the clips must teach, given
    /// directly instead of inferred from VideoDescription.</summary>
    public string Objective { get; set; } = string.Empty;
    public string VideoDescription { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string ImageDescription { get; set; } = string.Empty;
    public string? LanguageCode { get; set; }

    /// <summary>Optional user-requested cap on the number of clips (2026-09-08)
    /// — lets a reviewer ask for a shorter, summarized sequence (e.g. 6)
    /// instead of the default <see cref="StructuredLearningVideoOrchestrator.MaxVideosPerSection"/>.
    /// Clamped to [1, MaxVideosPerSection].</summary>
    public int? MaxVideos { get; set; }
}

public sealed class GenerateStructuredLearningVideoPromptResponse
{
    /// <summary>Ordered clip prompts (2026-08-25) — one entry if the topic
    /// fits in a single 10-second clip, several if the agent split it,
    /// never more than <see cref="StructuredLearningVideoOrchestrator.MaxVideosPerSection"/>.</summary>
    public List<string> RecommendedPrompts { get; set; } = [];
}

/// <summary>Generates the final Veo prompt for review after the three video
/// wizard inputs are complete. This endpoint does not generate a video.</summary>
public sealed class GenerateStructuredLearningVideoPromptFunction
{
    private readonly StructuredLearningVideoPromptAgent _agent;

    public GenerateStructuredLearningVideoPromptFunction(StructuredLearningVideoPromptAgent agent)
    {
        _agent = agent;
    }

    [Function("GenerateStructuredLearningVideoPrompt")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/video-prompt/generate")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "VideoPromptAgentNotConfigured",
                "Video prompt generation is not configured.", cancellationToken);
        }

        GenerateStructuredLearningVideoPromptRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<GenerateStructuredLearningVideoPromptRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Objective) || string.IsNullOrWhiteSpace(body.VideoDescription) ||
            string.IsNullOrWhiteSpace(body.Environment) || string.IsNullOrWhiteSpace(body.ImageDescription))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields",
                "Objective, VideoDescription, Environment, and ImageDescription are required.", cancellationToken);
        }

        var maxVideos = body.MaxVideos is int requested
            ? Math.Clamp(requested, 1, StructuredLearningVideoOrchestrator.MaxVideosPerSection)
            : StructuredLearningVideoOrchestrator.MaxVideosPerSection;

        try
        {
            var prompts = await _agent.GenerateAsync(
                body.Objective, body.VideoDescription, body.Environment, body.ImageDescription,
                maxVideos, body.LanguageCode, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(
                request, new GenerateStructuredLearningVideoPromptResponse { RecommendedPrompts = prompts },
                cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "VideoPromptGenerationFailed", ex.Message, cancellationToken);
        }
    }
}
