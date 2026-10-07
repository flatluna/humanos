using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

public sealed class PostStructuredLearningVoiceSdpRequest
{
    public string ClientSecret { get; set; } = string.Empty;

    public string Sdp { get; set; } = string.Empty;

    public bool UsesTools { get; set; }

    public Guid? PersonId { get; set; }

    public Guid? EngramSessionId { get; set; }
}

public sealed class PostStructuredLearningVoiceSdpResponse
{
    public string Sdp { get; set; } = string.Empty;
}

/// <summary>
/// POST structured-learning/sections/steps/{stepId}/voice-session/sdp
/// (2026-10-02) \u2014 the browser sends its SDP offer HERE instead of straight
/// to Azure, so the backend can learn the call's call_id and open its own
/// token-usage observer (see RealtimeVoiceUsageObserverService). The
/// browser's own WebRTC connection is otherwise unaffected \u2014 media/data
/// channel still negotiate peer-to-peer browser\u2194Azure once it gets this
/// SDP answer back.
/// </summary>
public sealed class PostStructuredLearningVoiceSdpFunction
{
    private readonly HumanOsDbContext _dbContext;
    private readonly RealtimeVoiceUsageObserverService _observerService;
    private readonly RealtimeVoiceSessionService _voiceSessionService;

    public PostStructuredLearningVoiceSdpFunction(
        HumanOsDbContext dbContext,
        RealtimeVoiceUsageObserverService observerService,
        RealtimeVoiceSessionService voiceSessionService)
    {
        _dbContext = dbContext;
        _observerService = observerService;
        _voiceSessionService = voiceSessionService;
    }

    [Function("PostStructuredLearningVoiceSdp")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "structured-learning/sections/steps/{stepId:guid}/voice-session/sdp")]
        HttpRequestData request,
        Guid stepId,
        CancellationToken cancellationToken)
    {
        if (!_voiceSessionService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "VoiceTutorNotConfigured", "The voice Tutor is not configured.", cancellationToken);
        }

        PostStructuredLearningVoiceSdpRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<PostStructuredLearningVoiceSdpRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.ClientSecret) || string.IsNullOrWhiteSpace(body.Sdp))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "MissingFields", "ClientSecret and Sdp are required.", cancellationToken);
        }

        var step = await _dbContext.StructuredLearningSectionSteps
            .AsNoTracking()
            .Include(s => s.Section)
            .FirstOrDefaultAsync(s => s.StructuredLearningSectionStepId == stepId, cancellationToken);

        if (step?.Section is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.NotFound,
                "StepNotFound", "No StructuredLearningSectionStep was found with that id.", cancellationToken);
        }

        var context = new RealtimeVoiceUsageContext
        {
            PersonId = body.PersonId,
            DocumentId = step?.Section?.StructuredLearningDocumentId,
            SectionId = step?.Section?.StructuredLearningDocumentSectionId,
            EngramSessionId = body.EngramSessionId,
            ModelName = _voiceSessionService.DeploymentName,
            StepType = step?.StepType.ToString(),
        };

        try
        {
            var answerSdp = await _observerService.NegotiateAndObserveAsync(
                body.ClientSecret, body.Sdp, body.UsesTools, context, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request, new PostStructuredLearningVoiceSdpResponse { Sdp = answerSdp }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadGateway, "VoiceSdpNegotiationFailed", ex.Message, cancellationToken);
        }
    }
}
