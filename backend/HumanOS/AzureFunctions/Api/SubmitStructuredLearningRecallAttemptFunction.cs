using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class SubmitStructuredLearningRecallAttemptRequest
{
    /// <summary>Leave null/empty ONLY on the very first call for a section
    /// (nothing to grade yet — just fetches the first question).</summary>
    public string? StudentResponse { get; set; }
}

public sealed class SubmitStructuredLearningRecallAttemptResponse
{
    public bool Started { get; set; }
    public string NextQuestion { get; set; } = string.Empty;
    public int? Score { get; set; }
    public int AttemptsUsedForItem { get; set; }
    public int ItemsMastered { get; set; }
    public int ItemsRequired { get; set; }
    public bool MasteredThisAttempt { get; set; }
    public bool Complete { get; set; }
    public bool ItemFailed { get; set; }
}

/// <summary>
/// Real-time Recall (2026-08-10) — the ONLY entry point for Recall
/// practice on a Structured Learning section. Grades the answer to the
/// question asked LAST turn (if any) and generates the NEXT question in
/// the SAME call — mirrors TutorSubmitRecallAttemptFunction's Graph
/// counterpart. See <see cref="StructuredLearningRecallEngine"/>'s doc
/// comment.
/// </summary>
public sealed class SubmitStructuredLearningRecallAttemptFunction
{
    private readonly StructuredLearningRecallEngine _engine;
    private readonly HumanOS.Agents.Studio.StructuredLearningRecallAgent _agent;
    private readonly HumanOsDbContext _dbContext;

    public SubmitStructuredLearningRecallAttemptFunction(
        StructuredLearningRecallEngine engine, HumanOS.Agents.Studio.StructuredLearningRecallAgent agent, HumanOsDbContext dbContext)
    {
        _engine = engine;
        _agent = agent;
        _dbContext = dbContext;
    }

    [Function("SubmitStructuredLearningRecallAttempt")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/recall/attempt")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StructuredLearningRecallAgentNotConfigured",
                "StructuredLearningRecallAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        SubmitStructuredLearningRecallAttemptRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SubmitStructuredLearningRecallAttemptRequest>(
                request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        try
        {
            var result = await _engine.SubmitAttemptAsync(_dbContext, sectionId, body?.StudentResponse, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(request, new SubmitStructuredLearningRecallAttemptResponse
            {
                Started = result.Started,
                NextQuestion = result.NextQuestion,
                Score = result.Score,
                AttemptsUsedForItem = result.AttemptsUsedForItem,
                ItemsMastered = result.ItemsMastered,
                ItemsRequired = result.ItemsRequired,
                MasteredThisAttempt = result.MasteredThisAttempt,
                Complete = result.Complete,
                ItemFailed = result.ItemFailed
            }, cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidRecallAttempt", ex.Message, cancellationToken);
        }
    }
}
