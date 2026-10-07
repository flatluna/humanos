using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class PostAiAgentVoiceUsageRequest
{
    public Guid? PersonId { get; set; }

    public Guid? DocumentId { get; set; }

    public int? DurationSeconds { get; set; }
}

/// <summary>
/// POST ai-agents/voice-usage (2026-10-04) — the browser reports how long an
/// /ai-agents "Talk about this" Realtime voice call lasted once it ends.
/// Mirrors <see cref="PostStructuredLearningEngramVoiceUsageFunction"/> but
/// for the AI Agents chat page's free-roaming voice follow-up, which has NO
/// StructuredLearningSectionStep to anchor on (see VoiceTutorAgent.tsx's
/// aiAgentQuestion mode — that path previously never reported usage at all,
/// a confirmed measurement gap). Logged under StepType="AiAgentChat" (never
/// "Teaching"), so it never pollutes the pedagogy-flow Learn-stage cost
/// baseline with unrelated free-roaming chat usage. See
/// <see cref="StructuredLearningEngramUsageService"/>.
/// </summary>
public sealed class PostAiAgentVoiceUsageFunction
{
    private readonly StructuredLearningEngramUsageService _usageService;
    private readonly RealtimeVoiceSessionService _voiceSessionService;

    public PostAiAgentVoiceUsageFunction(
        StructuredLearningEngramUsageService usageService,
        RealtimeVoiceSessionService voiceSessionService)
    {
        _usageService = usageService;
        _voiceSessionService = voiceSessionService;
    }

    [Function("PostAiAgentVoiceUsage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ai-agents/voice-usage")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        PostAiAgentVoiceUsageRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<PostAiAgentVoiceUsageRequest>(
                request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        var durationSeconds = body?.DurationSeconds ?? 0;
        if (durationSeconds <= 0)
        {
            return await FunctionResponseFactory.SuccessResponseAsync(request, new { logged = false }, cancellationToken: cancellationToken);
        }

        await _usageService.RecordVoiceUsageAsync(
            body?.PersonId,
            body?.DocumentId,
            sectionId: null,
            sessionId: null,
            modelName: _voiceSessionService.DeploymentName,
            durationSeconds,
            inputTokens: 0,
            outputTokens: 0,
            cancellationToken,
            stepType: "AiAgentChat");

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { logged = true }, cancellationToken: cancellationToken);
    }
}
