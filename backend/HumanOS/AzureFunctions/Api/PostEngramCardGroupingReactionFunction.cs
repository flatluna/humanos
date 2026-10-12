using System.Net;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

public sealed class PostEngramCardGroupingReactionRequest
{
    /// <summary>The correct option's Title (e.g. "La vaca").</summary>
    public string CorrectTitle { get; set; } = string.Empty;

    /// <summary>The correct option's RecognitionCue text (e.g. "Mu, mu") —
    /// what was actually played for this round, so the reaction can
    /// naturally reference it. Null if unavailable.</summary>
    public string? CorrectCue { get; set; }

    /// <summary>What the child actually tapped (e.g. "El gato").</summary>
    public string TappedTitle { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    public string Language { get; set; } = "es";
}

/// <summary>
/// Stateless, DB-free endpoint (2026-10-08) for the Grouping card's
/// recognition game (see <see cref="HumanOS.Models.Capabilities.Studio.EngramCard"/>'s
/// RecognitionCue/Children docs) — the frontend already knows, client-side,
/// deterministically, whether a tap was correct (EngramGroupingGame's own
/// comparison); this endpoint ONLY phrases Profe's warm spoken reaction via
/// <see cref="StructuredLearningEngramCardGameReactionAgent"/> and
/// synthesizes it to audio on the fly, returned directly as the response
/// body (never persisted to Data Lake — this feedback is ephemeral, not
/// part of the Engram's durable content). "Step 1" (text+TTS) of a possible
/// later "step 2" (live Realtime voice session) for this same game.
/// </summary>
public sealed class PostEngramCardGroupingReactionFunction
{
    private readonly StructuredLearningEngramCardGameReactionAgent _agent;
    private readonly AzureSpeechService _speechService;
    private readonly ILogger<PostEngramCardGroupingReactionFunction> _logger;

    public PostEngramCardGroupingReactionFunction(
        StructuredLearningEngramCardGameReactionAgent agent,
        AzureSpeechService speechService,
        ILogger<PostEngramCardGroupingReactionFunction> logger)
    {
        _agent = agent;
        _speechService = speechService;
        _logger = logger;
    }

    [Function("PostEngramCardGroupingReaction")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/engram-cards/grouping-reaction")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured || !_speechService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.ServiceUnavailable,
                "GroupingReactionNotConfigured",
                "The Grouping game reaction agent or speech service is not configured.",
                cancellationToken);
        }

        PostEngramCardGroupingReactionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<PostEngramCardGroupingReactionRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.CorrectTitle) || string.IsNullOrWhiteSpace(body.TappedTitle))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "CorrectTitle and TappedTitle are required.", cancellationToken);
        }

        try
        {
            var reactionText = await _agent.ReactAsync(
                body.CorrectTitle, body.CorrectCue, body.TappedTitle, body.IsCorrect, body.Language, cancellationToken);

            var voiceName = string.Equals(body.Language, "es", StringComparison.OrdinalIgnoreCase)
                ? "es-MX-JorgeNeural"
                : "en-US-AndrewNeural";
            var audioBytes = await _speechService.SynthesizeAsync(reactionText, voiceName, cancellationToken);

            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "audio/mpeg");
            response.Headers.Add("Cache-Control", "no-store");
            response.Headers.Add("X-Reaction-Text", Uri.EscapeDataString(reactionText));
            await response.Body.WriteAsync(audioBytes, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Grouping game reaction audio");
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.InternalServerError, "GroupingReactionFailed", ex.Message, cancellationToken);
        }
    }
}
