using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class StructuredLearningEngramVoiceUsageRequest
{
    public Guid? PersonId { get; set; }

    public int? DurationSeconds { get; set; }

    public int? InputTokens { get; set; }

    public int? OutputTokens { get; set; }

    /// <summary>The real engram visit (StructuredLearningEngramSession) this
    /// call happened during (2026-10-02). Null if the frontend hasn't
    /// started a session yet.</summary>
    public Guid? EngramSessionId { get; set; }
}

/// <summary>
/// POST structured-learning/sections/steps/{stepId}/voice-usage (2026-10-01)
/// — the browser reports how long a "Q&A with Professor" Voice call lasted
/// once it ends (Realtime connects browser-to-Azure directly over WebRTC,
/// so the backend never otherwise sees the real duration — same
/// client-reports pattern as PostBuddySessionUsageFunction). See
/// <see cref="StructuredLearningEngramUsageService"/>.
/// </summary>
public sealed class PostStructuredLearningEngramVoiceUsageFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly StructuredLearningEngramUsageService _usageService;
    private readonly RealtimeVoiceSessionService _voiceSessionService;

    public PostStructuredLearningEngramVoiceUsageFunction(
        HumanOsDbContext dbContext,
        StructuredLearningEngramUsageService usageService,
        RealtimeVoiceSessionService voiceSessionService)
    {
        _dbContext = dbContext;
        _usageService = usageService;
        _voiceSessionService = voiceSessionService;
    }

    [Function("PostStructuredLearningEngramVoiceUsage")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "structured-learning/sections/steps/{stepId:guid}/voice-usage")]
        HttpRequestData request,
        Guid stepId,
        CancellationToken cancellationToken)
    {
        StructuredLearningEngramVoiceUsageRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<StructuredLearningEngramVoiceUsageRequest>(
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
        var inputTokens = body?.InputTokens ?? 0;
        var outputTokens = body?.OutputTokens ?? 0;
        if (durationSeconds < 0 || inputTokens < 0 || outputTokens < 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidUsage", "Usage values cannot be negative.", cancellationToken);
        }

        if (durationSeconds == 0 && inputTokens == 0 && outputTokens == 0)
        {
            // Nothing meaningful to log (e.g. the call never connected and no usage was reported).
            return await FunctionResponseFactory.SuccessResponseAsync(request, new { logged = false }, cancellationToken: cancellationToken);
        }

        var step = await _dbContext.StructuredLearningSectionSteps
            .AsNoTracking()
            .Include(s => s.Section)
            .FirstOrDefaultAsync(s => s.StructuredLearningSectionStepId == stepId, cancellationToken);

        if (step?.Section is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "StepNotFound", "No StructuredLearningSectionStep was found with that id.", cancellationToken);
        }

        await _usageService.RecordVoiceUsageAsync(
            body?.PersonId,
            step.Section.StructuredLearningDocumentId,
            step.Section.StructuredLearningDocumentSectionId,
            body?.EngramSessionId,
            modelName: _voiceSessionService.DeploymentName,
            durationSeconds,
            inputTokens,
            outputTokens,
            cancellationToken,
            stepType: step.StepType.ToString());

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { logged = true }, cancellationToken: cancellationToken);
    }
}
