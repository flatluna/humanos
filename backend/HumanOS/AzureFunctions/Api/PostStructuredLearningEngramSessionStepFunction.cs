using System.Net;
using System.Text.Json;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>Cumulative visible time for one browser-generated interval.</summary>
public sealed record RecordStructuredLearningStepActivityRequest
{
    public Guid IntervalId { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public bool Stopped { get; init; }
}

/// <summary>
/// POST structured-learning/engram-sessions/{sessionId}/sections/{sectionId}/steps/{stepType}/start
/// and .../complete (2026-10-02) — records when the student started/
/// finished each of the 6 steps (Hypothesis/Teaching/DeepDive/Production/
/// Recall/Assessment) for a given engram (sectionId) within one COURSE
/// session. The session itself spans every engram visited — it only ends
/// via PostStructuredLearningEngramSessionCloseFunction. See
/// <see cref="StructuredLearningEngramSessionService"/>.
/// </summary>
public sealed class PostStructuredLearningEngramSessionStepFunction
{
    private static readonly HashSet<string> StepTypes = ["Hypothesis", "Teaching", "DeepDive", "Production", "Recall", "Assessment"];
    private readonly StructuredLearningEngramSessionService _sessionService;

    public PostStructuredLearningEngramSessionStepFunction(StructuredLearningEngramSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [Function("PostStructuredLearningEngramStepActivity")]
    public async Task<HttpResponseData> ActivityAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "structured-learning/engram-sessions/{sessionId:guid}/sections/{sectionId:guid}/steps/{stepType}/activity")]
        HttpRequestData request, Guid sessionId, Guid sectionId, string stepType, CancellationToken cancellationToken)
    {
        RecordStructuredLearningStepActivityRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<RecordStructuredLearningStepActivityRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest,
                "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }
        if (body is null || body.IntervalId == Guid.Empty || body.ElapsedMilliseconds < 0
            || body.ElapsedMilliseconds > (long)int.MaxValue * 1000 || !StepTypes.Contains(stepType))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.BadRequest,
                "InvalidActivity", "A valid step, interval ID and non-negative supported duration are required.", cancellationToken);
        }
        try
        {
            await _sessionService.RecordStepActivityAsync(sessionId, sectionId, stepType,
                body.IntervalId, body.ElapsedMilliseconds, body.Stopped, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.NotFound,
                "SessionNotFound", "Course session not found.", cancellationToken);
        }
        catch (ArgumentException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(request, HttpStatusCode.Conflict,
                "IntervalConflict", "The interval does not belong to this step.", cancellationToken);
        }
        return await FunctionResponseFactory.SuccessResponseAsync(request, new { logged = true }, cancellationToken: cancellationToken);
    }

    [Function("PostStructuredLearningEngramSessionStepStart")]
    public async Task<HttpResponseData> StartAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "structured-learning/engram-sessions/{sessionId:guid}/sections/{sectionId:guid}/steps/{stepType}/start")]
        HttpRequestData request,
        Guid sessionId,
        Guid sectionId,
        string stepType,
        CancellationToken cancellationToken)
    {
        await _sessionService.RecordStepStartAsync(sessionId, sectionId, stepType, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, new { logged = true }, cancellationToken: cancellationToken);
    }

    [Function("PostStructuredLearningEngramSessionStepComplete")]
    public async Task<HttpResponseData> CompleteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "structured-learning/engram-sessions/{sessionId:guid}/sections/{sectionId:guid}/steps/{stepType}/complete")]
        HttpRequestData request,
        Guid sessionId,
        Guid sectionId,
        string stepType,
        CancellationToken cancellationToken)
    {
        await _sessionService.RecordStepCompleteAsync(sessionId, sectionId, stepType, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, new { logged = true }, cancellationToken: cancellationToken);
    }
}
